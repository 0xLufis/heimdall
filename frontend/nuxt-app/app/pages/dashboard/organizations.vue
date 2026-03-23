<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h3 class="text-2xl font-black text-gray-900 tracking-tight font-sans">Organizations</h3>
        <p class="text-sm text-gray-500 mt-1 font-medium font-sans">Manage teams, departments, and project groups.</p>
      </div>
      <button @click="showCreateModal = true" class="inline-flex items-center gap-2 px-5 py-2.5 bg-indigo-600 rounded-xl text-sm font-bold text-white hover:bg-indigo-700 transition-all shadow-lg hover:shadow-indigo-200">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
        </svg>
        New Organization
      </button>
    </div>

    <!-- Organizations Grid -->
    <div v-if="loading" class="flex flex-col items-center justify-center py-24 gap-4">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-600"></div>
        <p class="text-[10px] font-black text-gray-400 uppercase tracking-widest">Fetching Groups...</p>
    </div>

    <div v-else-if="orgs.length === 0" class="bg-white border-2 border-dashed border-gray-200 rounded-3xl p-16 text-center">
        <div class="w-20 h-20 bg-gray-50 rounded-2xl flex items-center justify-center mx-auto mb-6">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-10 w-10 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
        </div>
        <h4 class="text-xl font-black text-gray-900 font-sans">No organizations yet</h4>
        <p class="text-gray-500 mt-2 max-w-sm mx-auto font-sans">Create your first organization to start grouping users and managing permissions at scale.</p>
        <button @click="showCreateModal = true" class="mt-8 px-6 py-2.5 bg-gray-900 text-white rounded-xl text-sm font-bold hover:bg-gray-800 transition-all shadow-md">
            Get Started
        </button>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <DashboardOrgCard 
          v-for="org in orgs" 
          :key="org.id" 
          :org="org" 
          @manage-members="handleManageMembers" 
        />
    </div>

    <!-- Create Org Modal -->
    <div v-if="showCreateModal" class="fixed inset-0 bg-gray-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in duration-200">
            <div class="p-8">
                <div class="flex items-center justify-between mb-6">
                    <h4 class="text-xl font-black text-gray-900">Create Organization</h4>
                    <button @click="showCreateModal = false" class="text-gray-400 hover:text-gray-600 transition-colors">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
                    </button>
                </div>
                <form @submit.prevent="handleCreateOrg" class="space-y-4">
                    <div>
                        <label class="block text-[10px] font-black text-gray-400 uppercase tracking-widest mb-1 ml-1">Organization Name</label>
                        <input v-model="newOrgName" type="text" required placeholder="e.g. Engineering Team" class="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all" />
                    </div>
                    <div>
                        <label class="block text-[10px] font-black text-gray-400 uppercase tracking-widest mb-1 ml-1">Slug (URL Identifier)</label>
                        <input v-model="newOrgSlug" type="text" required placeholder="engineering-team" class="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all font-mono text-sm" />
                    </div>
                    <button type="submit" :disabled="creating" class="w-full py-3.5 bg-indigo-600 text-white rounded-xl font-black shadow-lg shadow-indigo-100 hover:bg-indigo-700 transition-all disabled:opacity-50 mt-4">
                        {{ creating ? 'Creating...' : 'Create Organization' }}
                    </button>
                </form>
            </div>
        </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { authClient } from "~/utils/auth-client"

definePageMeta({
  layout: 'shadcn-dashboard'
})

const loading = ref(true)
const creating = ref(false)
const showCreateModal = ref(false)
const orgs = ref<any[]>([])

const newOrgName = ref('')
const newOrgSlug = ref('')

async function fetchOrgs() {
    loading.value = true
    try {
        const { data, error } = await authClient.organization.list()
        if (data) orgs.value = data
    } catch (e) {
        console.error(e)
    } finally {
        loading.value = false
    }
}

async function handleCreateOrg() {
    creating.value = true
    try {
        const { data, error } = await authClient.organization.create({
            name: newOrgName.value,
            slug: newOrgSlug.value
        })
        if (error) {
            alert(error.message)
        } else {
            showCreateModal.value = false
            newOrgName.value = ''
            newOrgSlug.value = ''
            await fetchOrgs()
        }
    } catch (e) {
        alert("Failed to create organization")
    } finally {
        creating.value = false
    }
}

function handleManageMembers(id: string) {
    alert(`Managing members for org: ${id}`)
}

watch(newOrgName, (val) => {
    newOrgSlug.value = val.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
})

onMounted(() => {
    fetchOrgs()
})
</script>
