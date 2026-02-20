import * as signalR from "@microsoft/signalr"

const apiBaseUrl = (import.meta as any).env?.VITE_API_BASE_URL ??
  "https://localhost:7179/api"

const hubUrl = apiBaseUrl.replace(/\/api\/?$/, "") + "/hub"

let connection: signalR.HubConnection | null = null

export function getHubConnection() {
  if (connection) return connection

  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => localStorage.getItem("token") ?? "",
    })
    .withAutomaticReconnect()
    .build()

  return connection
}

export async function startHub() {
  const hub = getHubConnection()
  if (hub.state === signalR.HubConnectionState.Disconnected) {
    await hub.start()
  }
}

export async function stopHub() {
  if (!connection) return
  if (connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop()
  }
}
