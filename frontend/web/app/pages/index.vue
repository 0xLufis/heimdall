<script setup lang="ts">
// Industrial landing page for Heimdall
</script>

<template>
  <div class="min-h-screen bg-[#0c0e12] text-zinc-100 font-sans selection:bg-zinc-700 selection:text-white">
    <!-- Navigation -->
    <nav class="bg-[#15181e]/90 backdrop-blur-md border-b border-[#232730] shadow-sm sticky top-0 z-50">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between h-16 items-center">
          <div class="flex items-center gap-2.5">
            <div class="w-8 h-8 bg-zinc-800 border border-white/10 rounded-lg flex items-center justify-center text-zinc-100 font-bold shadow-sm">H</div>
            <span class="text-xl font-bold tracking-tight text-zinc-100">Heimdall</span>
          </div>
          <div class="hidden md:flex items-center space-x-8 text-sm font-medium">
            <a href="#" class="text-zinc-400 hover:text-zinc-200 transition-colors">Features</a>
            <a href="#" class="text-zinc-400 hover:text-zinc-200 transition-colors">Architecture</a>
            <a href="#" class="text-zinc-400 hover:text-zinc-200 transition-colors">Docs</a>
            <NuxtLink to="/auth/login" class="bg-zinc-700 hover:bg-zinc-600 text-white border border-zinc-600/50 px-4 py-2 rounded-lg transition-colors text-xs font-semibold shadow-sm">
              Get Started
            </NuxtLink>
          </div>
        </div>
      </div>
    </nav>

    <main>
      <!-- Hero Section -->
      <section class="py-20 lg:py-32">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h1 class="text-5xl lg:text-6xl font-extrabold text-zinc-100 mb-6 tracking-tight">
            Industrial PC Management <span class="text-zinc-400">Perfected</span>
          </h1>
          <p class="text-lg text-zinc-400 max-w-2xl mx-auto mb-10 leading-relaxed">
            Heimdall is an industrial-grade management system designed to track, configure, and monitor client PCs operating in production environments.
          </p>
          <div class="flex flex-col sm:flex-row justify-center gap-4">
            <NuxtLink to="/auth/signup" class="bg-zinc-700 hover:bg-zinc-600 text-white border border-zinc-600/50 px-8 py-3 rounded-xl font-semibold text-sm transition-all shadow-md">
              Start Monitoring
            </NuxtLink>
            <NuxtLink to="/auth/login" class="bg-[#15181e] text-zinc-300 border border-[#232730] hover:border-zinc-700 hover:text-white px-8 py-3 rounded-xl font-semibold text-sm transition-colors">
              Sign In
            </NuxtLink>
          </div>
        </div>
      </section>

      <!-- Features Grid -->
      <section class="py-20 bg-[#0e1116] border-t border-b border-[#232730]">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="text-center mb-16">
            <h2 class="text-3xl font-bold text-zinc-100 mb-3 tracking-tight">Core Capabilities</h2>
            <p class="text-zinc-400 text-sm">Built for the demanding needs of modern production lines.</p>
          </div>
          
          <div class="grid grid-cols-1 md:grid-cols-3 gap-8">
            <div class="p-8 bg-[#15181e] rounded-2xl border border-[#232730] space-y-4">
              <div class="w-12 h-12 bg-zinc-800 text-zinc-300 border border-zinc-700 rounded-xl flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z" />
                </svg>
              </div>
              <h3 class="text-lg font-bold text-zinc-100">Hardware Tracking</h3>
              <p class="text-xs text-zinc-400 leading-relaxed">Detailed monitoring of CPU, RAM, and storage using native WMI and FS interrogation.</p>
            </div>

            <div class="p-8 bg-[#15181e] rounded-2xl border border-[#232730] space-y-4">
              <div class="w-12 h-12 bg-zinc-800 text-zinc-300 border border-zinc-700 rounded-xl flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
                </svg>
              </div>
              <h3 class="text-lg font-bold text-zinc-100">Software Management</h3>
              <p class="text-xs text-zinc-400 leading-relaxed">Track installed packages and OS versions across your entire fleet of industrial PCs.</p>
            </div>

            <div class="p-8 bg-[#15181e] rounded-2xl border border-[#232730] space-y-4">
              <div class="w-12 h-12 bg-zinc-800 text-zinc-300 border border-zinc-700 rounded-xl flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2.21 3.58 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.58 4 8 4s8-1.79 8-4M4 7c0-2.21 3.58-4 8-4s8 1.79 8 4m0 5c0 2.21-3.58-4-8 4s-8-1.79-8-4" />
                </svg>
              </div>
              <h3 class="text-lg font-bold text-zinc-100">Hybrid Database</h3>
              <p class="text-xs text-zinc-400 leading-relaxed">Powered by PostgreSQL JSONB for a flexible yet structured data model.</p>
            </div>
          </div>
        </div>
      </section>
    </main>

    <footer class="bg-[#0c0e12] border-t border-[#232730] text-zinc-400 py-12">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col md:flex-row justify-between items-center gap-8">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 bg-zinc-800 border border-white/10 rounded-lg flex items-center justify-center text-zinc-100 font-bold shadow-sm">H</div>
          <span class="text-xl font-bold text-zinc-100">Heimdall</span>
        </div>
        <p class="text-zinc-500 text-xs">© 2026 Heimdall Management System. All rights reserved.</p>
        <div class="flex gap-6 text-xs">
          <a href="#" class="text-zinc-500 hover:text-zinc-300 transition-colors">Privacy</a>
          <a href="#" class="text-zinc-500 hover:text-zinc-300 transition-colors">Terms</a>
        </div>
      </div>
    </footer>
  </div>
</template>
