<template>
  <div>
    <h1>Management System</h1>
    <div style="display: flex; gap: 12px; flex-wrap: wrap; margin: 8px 0">
      <a href="/dashboard">Dashboard</a>
      <a href="/crash-game/overview">Crash Game</a>
      <a href="/telegram-posts">Telegram Posts</a>
      <a href="/telegram-crawl-sources">Crawl Sources</a>
    </div>
    <router-view />
  </div>
</template>
