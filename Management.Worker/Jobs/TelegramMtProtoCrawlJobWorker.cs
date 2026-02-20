using Microsoft.Extensions.Options;
using Management.Worker.Services;
using TL;
using WTelegram;
using System.Diagnostics;

namespace Management.Worker.Jobs;

public sealed class TelegramMtProtoCrawlJobWorker : BackgroundService
{
    private readonly ILogger<TelegramMtProtoCrawlJobWorker> _logger;
    private readonly TelegramCrawlJobOptions _jobOpts;
    private readonly TelegramMtProtoOptions _mtOpts;
    private readonly TelegramUploaderOptions _uploaderOpts;
    private readonly TelegramVideoDemoOptions _videoDemoOpts;
    private readonly TelegramPublicChannelUploader _uploader;
    private readonly Management.Infrastructure.Repositories.TelegramCrawlRepository _crawlRepo;
    private readonly TelegramMtProtoClientProvider _clientProvider;
    private readonly Management.Infrastructure.Repositories.TelegramPostRepository _postRepo;

    public TelegramMtProtoCrawlJobWorker(
        ILogger<TelegramMtProtoCrawlJobWorker> logger,
        IOptions<TelegramCrawlJobOptions> jobOpts,
        IOptions<TelegramMtProtoOptions> mtOpts,
        IOptions<TelegramUploaderOptions> uploaderOpts,
        IOptions<TelegramVideoDemoOptions> videoDemoOpts,
        TelegramPublicChannelUploader uploader,
        Management.Infrastructure.Repositories.TelegramCrawlRepository crawlRepo,
        TelegramMtProtoClientProvider clientProvider,
        Management.Infrastructure.Repositories.TelegramPostRepository postRepo)
    {
        _logger = logger;
        _jobOpts = jobOpts.Value;
        _mtOpts = mtOpts.Value;
        _uploaderOpts = uploaderOpts.Value;
        _videoDemoOpts = videoDemoOpts.Value;
        _uploader = uploader;
        _crawlRepo = crawlRepo;
        _clientProvider = clientProvider;
        _postRepo = postRepo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _uploader.EnsureTempFolder();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram MTProto crawl job failed");
            }

            var delay = TimeSpan.FromMinutes(Math.Max(1, _jobOpts.RunIntervalMinutes));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunOnce(CancellationToken cancellationToken)
    {
        if (_mtOpts.ApiId == 0 || string.IsNullOrWhiteSpace(_mtOpts.ApiHash) || string.IsNullOrWhiteSpace(_mtOpts.PhoneNumber))
        {
            _logger.LogWarning("TelegramMtProto is not configured (ApiId/ApiHash/PhoneNumber). Skip crawl.");
            return;
        }

        // Option B: sync dialogs -> upsert crawl sources
        await SyncDialogs(cancellationToken);

        var sources = await _crawlRepo.GetEnabledSources(cancellationToken);
        if (sources.Count == 0)
        {
            _logger.LogInformation("No enabled crawl sources. Skip crawl.");
            return;
        }

        _logger.LogInformation("Crawl job starting. Sources={Count}", sources.Count);

        if (!_uploader.IsConfigured)
        {
            _logger.LogWarning("TelegramUploader is not configured (BotToken/PublicChannelId). Skip crawling because cannot upload to public channel.");
            return;
        }

        using var client = _clientProvider.CreateClient();
        await client.LoginUserIfNeeded();

        foreach (var src in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CrawlSource(client, src, cancellationToken);
        }

        _logger.LogInformation("Crawl job finished");
    }

    private async Task CrawlSource(Client client, Management.Infrastructure.Repositories.TelegramCrawlRepository.CrawlSourceRow src, CancellationToken cancellationToken)
    {
        var peer = BuildInputPeer(src);
        if (peer is null)
        {
            _logger.LogWarning("Skip source {PeerType}:{PeerId} due to missing AccessHash", src.PeerType, src.PeerId);
            return;
        }

        var lastId = await _crawlRepo.GetLastMessageId(src.PeerType, src.PeerId, cancellationToken);
        var maxSeen = lastId;

        _logger.LogInformation("Crawling {PeerType}:{PeerId} lastId={LastId}", src.PeerType, src.PeerId, lastId);

        // We fetch history in pages, using min_id to stop at last processed
        var offsetId = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var history = await client.Messages_GetHistory(peer, offset_id: offsetId, min_id: 0, limit: _jobOpts.BatchSize);
            if (history.Messages is null || history.Messages.Length == 0)
            {
                break;
            }

            _logger.LogInformation(
                "Fetched history batch. peer={PeerType}:{PeerId} offset_id={OffsetId} min_id={MinId} count={Count} maxId={MaxId} minId={BatchMinId}",
                src.PeerType,
                src.PeerId,
                offsetId,
                lastId,
                history.Messages.Length,
                history.Messages.Max(m => m.ID),
                history.Messages.Min(m => m.ID));

            // WTelegram returns newest->oldest, we want to process older->newer for stable grouping/offset
            var messages = history.Messages
                .OfType<Message>()
                .Where(m => m.ID > lastId)
                .OrderBy(m => m.ID)
                .ToList();

            if (messages.Count == 0)
            {
                offsetId = history.Messages[^1].ID;
                continue;
            }

            foreach (var group in GroupByAlbum(messages))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var processedMax = await ProcessMessageGroup(client, src, group, cancellationToken);
                if (processedMax > maxSeen)
                {
                    maxSeen = processedMax;
                    await _crawlRepo.UpsertLastMessageId(src.PeerType, src.PeerId, maxSeen, DateTime.UtcNow, cancellationToken);
                    _logger.LogInformation("Checkpoint offset {PeerType}:{PeerId} -> {MaxSeen}", src.PeerType, src.PeerId, maxSeen);
                }
            }

            // next page
            offsetId = history.Messages[^1].ID;
        }

        if (maxSeen > lastId)
        {
            await _crawlRepo.UpsertLastMessageId(src.PeerType, src.PeerId, maxSeen, DateTime.UtcNow, cancellationToken);
            _logger.LogInformation("Updated offset {PeerType}:{PeerId} -> {MaxSeen}", src.PeerType, src.PeerId, maxSeen);
        }
    }

    private static InputPeer? BuildInputPeer(Management.Infrastructure.Repositories.TelegramCrawlRepository.CrawlSourceRow src)
    {
        return src.PeerType switch
        {
            "channel" => src.AccessHash.HasValue ? new InputPeerChannel(src.PeerId, src.AccessHash.Value) : null,
            "group" => new InputPeerChat(src.PeerId),
            _ => null
        };
    }

    private static IEnumerable<List<Message>> GroupByAlbum(IReadOnlyList<Message> messages)
    {
        // grouped_id is used for albums; 0 means not grouped
        var byKey = new Dictionary<long, List<Message>>();
        var singles = new List<List<Message>>();

        foreach (var m in messages)
        {
            if (m.grouped_id is long gid && gid != 0)
            {
                if (!byKey.TryGetValue(gid, out var list))
                {
                    list = new List<Message>();
                    byKey[gid] = list;
                }
                list.Add(m);
            }
            else
            {
                singles.Add(new List<Message> { m });
            }
        }

        foreach (var g in byKey.Values)
        {
            g.Sort((a, b) => a.ID.CompareTo(b.ID));
            yield return g;
        }

        foreach (var s in singles)
            yield return s;
    }

    private async Task<int> ProcessMessageGroup(Client client, Management.Infrastructure.Repositories.TelegramCrawlRepository.CrawlSourceRow src, IReadOnlyList<Message> group, CancellationToken cancellationToken)
    {
        var first = group[0];

        // ignore if already saved
        if (await _postRepo.ExistsByTelegramMessage(src.PeerId, first.ID))
        {
            return group.Max(m => m.ID);
        }

        var postId = Guid.NewGuid();
        var originalContent = first.message;
        var viewFullContent = BuildViewFullContent(postId);

        var medias = new List<Management.Infrastructure.Repositories.TelegramPostMediaRow>();
        var sort = 0;

        foreach (var msg in group)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (msg.media is MessageMediaPhoto)
            {
                // Skip non-video content
                continue;
            }

            if (msg.media is MessageMediaDocument { document: Document doc })
            {
                if (!IsVideo(doc))
                {
                    continue;
                }

                var ext = GetDocumentExtension(doc) ?? "mp4";
                var temp = _uploader.GetTempFilePath($"video_{src.PeerId}_{msg.ID}.{ext}");
                try
                {
                    await using (var fs = System.IO.File.Create(temp))
                    {
                        await client.DownloadFileAsync(doc, fs, progress: (p, t) => cancellationToken.ThrowIfCancellationRequested());
                    }

                    var duration = GetVideoDuration(doc);
                    var buttonUrl = sort == 0 ? GetBotDeepLink(postId) : null;

                    var shouldMakeDemo = _videoDemoOpts.Enabled
                        && duration.HasValue
                        && duration.Value >= _videoDemoOpts.MinFullSeconds;

                    if (shouldMakeDemo)
                    {
                        var demoSeconds = Math.Clamp(_videoDemoOpts.DemoSeconds, 1, Math.Max(1, duration.Value));
                        var demoPath = _uploader.GetTempFilePath($"demo_{src.PeerId}_{msg.ID}.mp4");
                        try
                        {
                            try
                            {
                                await TrimVideoAsync(
                                    inputPath: temp,
                                    outputPath: demoPath,
                                    seconds: demoSeconds,
                                    cancellationToken: cancellationToken);

                                var demoFileId = await _uploader.UploadVideoAsync(
                                    chatId: _uploaderOpts.PublicChannelId,
                                    localPath: demoPath,
                                    fileName: Path.GetFileName(demoPath),
                                    caption: null,
                                    duration: demoSeconds,
                                    buttonText: "VIEW FULL HERE",
                                    buttonUrl: buttonUrl,
                                    cancellationToken: cancellationToken);

                                medias.Add(new Management.Infrastructure.Repositories.TelegramPostMediaRow(
                                    MediaId: Guid.Empty,
                                    PostId: Guid.Empty,
                                    MediaType: "video",
                                    FileUrl: string.Empty,
                                    TelegramFileId: demoFileId,
                                    Duration: demoSeconds,
                                    FileSize: null,
                                    ThumbnailUrl: null,
                                    SortOrder: sort++,
                                    CreatedAt: default));

                                var storageChatId = _uploaderOpts.StorageChannelId != 0
                                    ? _uploaderOpts.StorageChannelId
                                    : _uploaderOpts.PublicChannelId;

                                var fullFileId = await _uploader.UploadVideoAsync(
                                    chatId: storageChatId,
                                    localPath: temp,
                                    fileName: Path.GetFileName(temp),
                                    caption: null,
                                    duration: duration,
                                    buttonText: null,
                                    buttonUrl: null,
                                    cancellationToken: cancellationToken);

                                medias.Add(new Management.Infrastructure.Repositories.TelegramPostMediaRow(
                                    MediaId: Guid.Empty,
                                    PostId: Guid.Empty,
                                    MediaType: "video_full",
                                    FileUrl: string.Empty,
                                    TelegramFileId: fullFileId,
                                    Duration: duration,
                                    FileSize: doc.size,
                                    ThumbnailUrl: null,
                                    SortOrder: sort++,
                                    CreatedAt: default));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Trim demo failed; uploading full video instead. peer={PeerId} msg={MsgId}", src.PeerId, msg.ID);

                                var publicFileId = await _uploader.UploadVideoAsync(
                                    chatId: _uploaderOpts.PublicChannelId,
                                    localPath: temp,
                                    fileName: Path.GetFileName(temp),
                                    caption: null,
                                    duration: duration,
                                    buttonText: "VIEW FULL HERE",
                                    buttonUrl: buttonUrl,
                                    cancellationToken: cancellationToken);

                                medias.Add(new Management.Infrastructure.Repositories.TelegramPostMediaRow(
                                    MediaId: Guid.Empty,
                                    PostId: Guid.Empty,
                                    MediaType: "video",
                                    FileUrl: string.Empty,
                                    TelegramFileId: publicFileId,
                                    Duration: duration,
                                    FileSize: doc.size,
                                    ThumbnailUrl: null,
                                    SortOrder: sort++,
                                    CreatedAt: default));

                                var storageChatId = _uploaderOpts.StorageChannelId != 0
                                    ? _uploaderOpts.StorageChannelId
                                    : _uploaderOpts.PublicChannelId;

                                var fullFileId = storageChatId == _uploaderOpts.PublicChannelId
                                    ? publicFileId
                                    : await _uploader.UploadVideoAsync(
                                        chatId: storageChatId,
                                        localPath: temp,
                                        fileName: Path.GetFileName(temp),
                                        caption: null,
                                        duration: duration,
                                        buttonText: null,
                                        buttonUrl: null,
                                        cancellationToken: cancellationToken);

                                medias.Add(new Management.Infrastructure.Repositories.TelegramPostMediaRow(
                                    MediaId: Guid.Empty,
                                    PostId: Guid.Empty,
                                    MediaType: "video_full",
                                    FileUrl: string.Empty,
                                    TelegramFileId: fullFileId,
                                    Duration: duration,
                                    FileSize: doc.size,
                                    ThumbnailUrl: null,
                                    SortOrder: sort++,
                                    CreatedAt: default));
                            }
                        }
                        finally
                        {
                            TryDelete(demoPath);
                        }
                    }
                    else
                    {
                        // Upload as-is to PUBLIC channel (full is short enough)
                        var fileId = await _uploader.UploadVideoAsync(
                            chatId: _uploaderOpts.PublicChannelId,
                            localPath: temp,
                            fileName: Path.GetFileName(temp),
                            caption: null,
                            duration: duration,
                            buttonText: "VIEW FULL HERE",
                            buttonUrl: buttonUrl,
                            cancellationToken: cancellationToken);

                        medias.Add(new Management.Infrastructure.Repositories.TelegramPostMediaRow(
                            MediaId: Guid.Empty,
                            PostId: Guid.Empty,
                            MediaType: "video_full",
                            FileUrl: string.Empty,
                            TelegramFileId: fileId,
                            Duration: duration,
                            FileSize: doc.size,
                            ThumbnailUrl: null,
                            SortOrder: sort++,
                            CreatedAt: default));

                        // For short videos, also show it in UI as main video
                        medias.Add(new Management.Infrastructure.Repositories.TelegramPostMediaRow(
                            MediaId: Guid.Empty,
                            PostId: Guid.Empty,
                            MediaType: "video",
                            FileUrl: string.Empty,
                            TelegramFileId: fileId,
                            Duration: duration,
                            FileSize: doc.size,
                            ThumbnailUrl: null,
                            SortOrder: sort++,
                            CreatedAt: default));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Video download/upload failed; skip this video. peer={PeerId} msg={MsgId}", src.PeerId, msg.ID);
                    continue;
                }
                finally
                {
                    TryDelete(temp);
                }
            }
        }

        var hasVideo = medias.Any(m => m.MediaType == "video" || m.MediaType == "video_full");
        if (!hasVideo)
        {
            return group.Max(m => m.ID);
        }

        var savedPostId = await _postRepo.InsertPostWithMedias(
            postId: postId,
            telegramChatId: src.PeerId,
            telegramMessageId: first.ID,
            title: null,
            originalContent: originalContent,
            content: viewFullContent,
            medias: medias,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Saved post {PostId} from {PeerType}:{PeerId} msg={MsgId} medias={MediaCount}", savedPostId, src.PeerType, src.PeerId, first.ID, medias.Count);
        return group.Max(m => m.ID);
    }

    private string BuildViewFullContent(Guid postId)
    {
        var url = GetBotDeepLink(postId);
        if (string.IsNullOrWhiteSpace(url)) return "VIEW FULL HERE";
        return $"VIEW FULL HERE: {url}";
    }

    private string? GetBotDeepLink(Guid postId)
    {
        var username = _uploaderOpts.BotUsername;
        if (string.IsNullOrWhiteSpace(username)) return null;

        var start = $"post_{postId:N}";
        return $"https://t.me/{username}?start={start}";
    }

    private async Task TrimVideoAsync(string inputPath, string outputPath, int seconds, CancellationToken cancellationToken)
    {
        var ffmpeg = string.IsNullOrWhiteSpace(_videoDemoOpts.FfmpegPath) ? "ffmpeg" : _videoDemoOpts.FfmpegPath;

        if (!Path.IsPathRooted(ffmpeg))
        {
            ffmpeg = Path.Combine(AppContext.BaseDirectory, ffmpeg);
        }

        async Task<(int ExitCode, string StdErr, string StdOut)> RunOnce(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Failed to start ffmpeg process");

            var stdErrTask = p.StandardError.ReadToEndAsync();
            var stdOutTask = p.StandardOutput.ReadToEndAsync();

            await p.WaitForExitAsync(cancellationToken);
            return (p.ExitCode, await stdErrTask, await stdOutTask);
        }

        var vf = "scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p";

        var args1 = $"-y -hide_banner -loglevel error -ss 0 -t {seconds} -i \"{inputPath}\" -vf \"{vf}\" -pix_fmt yuv420p -c:v libx264 -preset veryfast -crf 28 -c:a aac -b:a 128k -movflags +faststart \"{outputPath}\"";
        var r1 = await RunOnce(args1);
        if (r1.ExitCode == 0) return;

        var args2 = $"-y -hide_banner -loglevel error -ss 0 -t {seconds} -i \"{inputPath}\" -vf \"{vf}\" -pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -c:v libx264 -preset veryfast -crf 28 -c:a aac -b:a 128k -movflags +faststart \"{outputPath}\"";
        var r2 = await RunOnce(args2);
        if (r2.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed (exit={r2.ExitCode}). stderr={r2.StdErr} stdout={r2.StdOut}");
        }
    }

    private static bool IsVideo(Document doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.mime_type) && doc.mime_type.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (doc.attributes is null) return false;
        return doc.attributes.OfType<DocumentAttributeVideo>().Any();
    }

    private static int? GetVideoDuration(Document doc)
    {
        if (doc.attributes is null) return null;
        var a = doc.attributes.OfType<DocumentAttributeVideo>().FirstOrDefault();
        return a is null ? null : (int?)Math.Round(a.duration);
    }

    private static string? GetDocumentExtension(Document doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.mime_type) && doc.mime_type.Contains('/'))
        {
            return doc.mime_type[(doc.mime_type.IndexOf('/') + 1)..];
        }
        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private async Task SyncDialogs(CancellationToken cancellationToken)
    {
        using var client = _clientProvider.CreateClient();
        await client.LoginUserIfNeeded();

        // Fetch all dialogs the user can see
        var all = await client.Messages_GetAllDialogs();

        var now = DateTime.UtcNow;
        var upserts = new List<Management.Infrastructure.Repositories.TelegramCrawlRepository.CrawlSourceUpsertRow>(512);

        // Channels (including megagroups) are in all.chats as TL.Channel
        foreach (var c in all.chats.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (c is Channel channel)
            {
                var peerType = "channel";
                var username = string.IsNullOrWhiteSpace(channel.username) ? null : channel.username;

                upserts.Add(new Management.Infrastructure.Repositories.TelegramCrawlRepository.CrawlSourceUpsertRow(
                    PeerType: peerType,
                    PeerId: channel.id,
                    AccessHash: channel.access_hash,
                    PeerUsername: username,
                    Title: channel.title));
            }
            else if (c is Chat chat)
            {
                upserts.Add(new Management.Infrastructure.Repositories.TelegramCrawlRepository.CrawlSourceUpsertRow(
                    PeerType: "group",
                    PeerId: chat.id,
                    AccessHash: null,
                    PeerUsername: null,
                    Title: chat.title));
            }
        }

        // Deduplicate by (PeerType, PeerId)
        var dedup = upserts
            .GroupBy(x => (x.PeerType, x.PeerId))
            .Select(g => g.First())
            .ToList();

        await _crawlRepo.UpsertSources(dedup, now, cancellationToken);

        _logger.LogInformation("SyncDialogs: upserted {Count} sources from dialogs", dedup.Count);
    }
}
