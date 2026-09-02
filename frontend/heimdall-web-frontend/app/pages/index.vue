<script setup lang="ts">
// Basic landing page for Heimdall
</script>

<template>
  <div class="min-h-screen bg-gray-50 text-gray-900 font-sans">
    <!-- Navigation -->
    <nav class="bg-white shadow-sm sticky top-0 z-50">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between h-16 items-center">
          <div class="flex items-center gap-2">
            <div class="w-8 h-8 bg-indigo-600 rounded-lg flex items-center justify-center text-white font-bold">H</div>
            <span class="text-xl font-bold tracking-tight text-gray-900">Heimdall</span>
          </div>
          <div class="hidden md:flex items-center space-x-8 text-sm font-medium">
            <a href="#" class="text-gray-500 hover:text-indigo-600 transition-colors">Features</a>
            <a href="#" class="text-gray-500 hover:text-indigo-600 transition-colors">Architecture</a>
            <a href="#" class="text-gray-500 hover:text-indigo-600 transition-colors">Docs</a>
            <NuxtLink to="/auth/login" class="bg-indigo-600 text-white px-4 py-2 rounded-md hover:bg-indigo-700 transition-colors">
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
          <h1 class="text-5xl lg:text-6xl font-extrabold text-gray-900 mb-6">
            Industrial PC Management <span class="text-indigo-600">Perfected</span>
          </h1>
          <p class="text-xl text-gray-600 max-w-2xl mx-auto mb-10">
            Heimdall is an industrial-grade management system designed to track, configure, and monitor client PCs operating in production environments.
          </p>
          <div class="flex flex-col sm:flex-row justify-center gap-4">
            <NuxtLink to="/auth/signup" class="bg-indigo-600 text-white px-8 py-3 rounded-lg font-semibold text-lg hover:bg-indigo-700 transition-colors shadow-lg shadow-indigo-200">
              Start Monitoring
            </NuxtLink>
            <NuxtLink to="/auth/login" class="bg-white text-gray-700 border border-gray-300 px-8 py-3 rounded-lg font-semibold text-lg hover:bg-gray-50 transition-colors">
              Sign In
            </NuxtLink>
          </div>
        </div>
      </section>

      <!-- Features Grid -->
      <section class="py-20 bg-white">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="text-center mb-16">
            <h2 class="text-3xl font-bold text-gray-900 mb-4">Core Capabilities</h2>
            <p class="text-gray-600">Built for the demanding needs of modern production lines.</p>
          </div>
          
          <div class="grid grid-cols-1 md:grid-cols-3 gap-8">
            <div class="p-8 bg-gray-50 rounded-2xl border border-gray-100">
              <div class="w-12 h-12 bg-indigo-100 text-indigo-600 rounded-lg flex items-center justify-center mb-6">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z" />
                </svg>
              </div>
              <h3 class="text-xl font-bold mb-3">Hardware Tracking</h3>
              <p class="text-gray-600">Detailed monitoring of CPU, RAM, and storage using native WMI and FS interrogation.</p>
            </div>

            <div class="p-8 bg-gray-50 rounded-2xl border border-gray-100">
              <div class="w-12 h-12 bg-indigo-100 text-indigo-600 rounded-lg flex items-center justify-center mb-6">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
                </svg>
              </div>
              <h3 class="text-xl font-bold mb-3">Software Management</h3>
              <p class="text-gray-600">Track installed packages and OS versions across your entire fleet of industrial PCs.</p>
            </div>

            <div class="p-8 bg-gray-50 rounded-2xl border border-gray-100">
              <div class="w-12 h-12 bg-indigo-100 text-indigo-600 rounded-lg flex items-center justify-center mb-6">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2.21 3.58 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.58 4 8 4s8-1.79 8-4M4 7c0-2.21 3.58-4 8-4s8 1.79 8 4m0 5c0 2.21-3.58-4-8 4s-8-1.79-8-4" />
                </svg>
              </div>
              <h3 class="text-xl font-bold mb-3">Hybrid Database</h3>
              <p class="text-gray-600">Powered by PostgreSQL JSONB for a flexible yet structured data model.</p>
            </div>
          </div>
        </div>
      </section>
    </main>

    <footer class="bg-gray-900 text-white py-12">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col md:flex-row justify-between items-center gap-8">
        <div class="flex items-center gap-2">
          <div class="w-8 h-8 bg-indigo-600 rounded-lg flex items-center justify-center text-white font-bold">H</div>
          <span class="text-xl font-bold">Heimdall</span>
        </div>
        <p class="text-gray-400 text-sm">© 2026 Heimdall Management System. All rights reserved.</p>
        <div class="flex gap-6">
          <a href="#" class="text-gray-400 hover:text-white transition-colors">Privacy</a>
          <a href="#" class="text-gray-400 hover:text-white transition-colors">Terms</a>
        </div>
      </div>
    </footer>
  </div>
</template>
