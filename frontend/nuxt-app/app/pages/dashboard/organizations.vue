<template>
  <div class="space-y-8">
    <!-- Header Area -->
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Organizations</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">Multi-tenant unit management</p>
      </div>
      
      <div class="flex items-center gap-4">
        <Button @click="showCreateModal = true" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-6 py-6 h-auto shadow-xl transition-all group border-0">
          <PlusIcon class="h-5 w-5 mr-2 group-hover:rotate-90 transition-transform" />
          <span class="text-xs font-black uppercase tracking-widest">Create Unit</span>
        </Button>
      </div>
    </div>

    <!-- Organizations Grid -->
    <div v-if="loading && orgs.length === 0" class="flex flex-col items-center justify-center py-32 gap-4">
        <div class="animate-spin rounded-full h-12 w-12 border-4 border-indigo-500 border-t-transparent"></div>
        <p class="text-[10px] font-black text-slate-500 uppercase tracking-[0.3em] animate-pulse">Synchronizing Nodes</p>
    </div>

    <div v-else-if="orgs.length === 0" class="bg-slate-900/50 border-2 border-dashed border-slate-800 rounded-[2rem] p-20 text-center shadow-inner">
        <div class="w-24 h-24 bg-slate-950 rounded-3xl flex items-center justify-center mx-auto mb-8 border border-slate-800 shadow-2xl">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 text-slate-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
        </div>
        <h4 class="text-2xl font-black text-slate-100 uppercase tracking-tight">No Active Units</h4>
        <p class="text-slate-500 mt-3 max-w-sm mx-auto text-sm font-bold uppercase tracking-wide opacity-60">Initialize your first organization to establish secure operational boundaries.</p>
        <Button @click="showCreateModal = true" class="mt-10 bg-slate-100 text-slate-950 hover:bg-white rounded-2xl px-10 py-6 h-auto font-black text-xs uppercase tracking-widest shadow-2xl transition-all">
            Begin Provisioning
        </Button>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
        <DashboardOrgCard 
          v-for="org in orgs" 
          :key="org.id" 
          :org="org" 
          @manage-members="handleManageMembers"
          @edit="handleEditOrg"
        />
    </div>

    <!-- Create/Edit Org Modal -->
    <Dialog :open="showCreateModal" @update:open="(val) => !val && (showCreateModal = false)">
        <DialogContent class="max-w-lg bg-slate-900 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-3xl shadow-2xl">
            <DialogHeader class="bg-indigo-950 p-8 border-b border-slate-800">
                <DialogTitle class="text-2xl font-black uppercase tracking-tight flex items-center gap-3 text-slate-100">
                    <div class="p-2 bg-indigo-500/20 rounded-xl text-indigo-400">
                        <PlusIcon class="h-6 w-6" />
                    </div>
                    {{ editingOrg ? 'Update Unit' : 'New Organization' }}
                </DialogTitle>
                <DialogDescription class="text-indigo-400 text-[10px] font-black uppercase tracking-widest mt-2 opacity-80">
                    Defining operational parameters for system isolation.
                </DialogDescription>
            </DialogHeader>

            <form @submit.prevent="handleSubmitOrg" class="p-8 space-y-6">
                <div class="space-y-2">
                    <label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Identity Label</label>
                    <Input v-model="newOrgName" required placeholder="e.g. ALPHA-OPERATIONS" class="rounded-2xl h-14 border-slate-800 bg-slate-950 text-slate-200 focus:ring-indigo-500/20 font-bold uppercase tracking-wider" />
                </div>
                <div class="space-y-2">
                    <label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">System Slug (Read-only)</label>
                    <Input v-model="newOrgSlug" disabled class="rounded-2xl h-14 border-slate-800 bg-slate-900 text-slate-500 focus:ring-0 font-mono text-xs cursor-not-allowed" />
                </div>
                
                <div class="flex gap-4 pt-4">
                    <Button type="button" variant="ghost" @click="showCreateModal = false" class="flex-1 rounded-2xl h-14 text-[10px] font-black text-slate-500 uppercase tracking-widest hover:bg-slate-800 hover:text-slate-300 transition-all">
                        Abort
                    </Button>
                    <Button type="submit" :disabled="creating" class="flex-1 bg-indigo-600 text-white rounded-2xl h-14 text-[10px] font-black uppercase tracking-widest hover:bg-indigo-700 shadow-xl shadow-indigo-500/20 transition-all">
                        {{ creating ? 'Processing...' : (editingOrg ? 'Update Node' : 'Commit Unit') }}
                    </Button>
                </div>
            </form>
        </DialogContent>
    </Dialog>

    <!-- Manage Members Modal -->
    <Dialog :open="showMembersModal" @update:open="(val) => !val && (showMembersModal = false)">
        <DialogContent class="max-w-2xl bg-slate-900 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-3xl shadow-2xl">
            <DialogHeader class="bg-slate-950 p-8 border-b border-slate-800">
                <DialogTitle class="text-xl font-black uppercase tracking-[0.2em] flex items-center gap-3 text-slate-100">
                    <UsersIcon class="h-5 w-5 text-indigo-500" />
                    Unit Personnel
                </DialogTitle>
                <DialogDescription class="text-slate-500 text-[10px] font-black uppercase tracking-widest mt-2">
                    Active operatives assigned to: {{ selectedOrg?.name }}
                </DialogDescription>
            </DialogHeader>

            <div class="p-8">
                <div v-if="loadingMembers" class="flex flex-col items-center justify-center py-12 gap-3">
                    <div class="w-8 h-8 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin"></div>
                    <span class="text-[9px] font-black text-slate-600 uppercase tracking-widest">Retrieving Roster...</span>
                </div>
                <div v-else class="space-y-4 max-h-[400px] overflow-y-auto pr-2 custom-scrollbar">
                    <div v-for="member in members" :key="member.id" class="flex items-center justify-between p-4 bg-slate-950 border border-slate-800 rounded-2xl group hover:border-indigo-500/30 transition-all">
                        <div class="flex items-center gap-4">
                            <div class="w-10 h-10 rounded-xl bg-slate-800 flex items-center justify-center text-xs font-black text-slate-400 uppercase">
                                {{ member.user.name.charAt(0) }}
                            </div>
                            <div>
                                <p class="text-sm font-black text-slate-200 uppercase tracking-tight">{{ member.user.name }}</p>
                                <p class="text-[10px] font-bold text-slate-500 uppercase tracking-tighter">{{ member.role }}</p>
                            </div>
                        </div>
                        <Button variant="ghost" size="icon" class="h-8 w-8 text-slate-600 hover:text-rose-500 opacity-0 group-hover:opacity-100 transition-all">
                            <Trash2Icon class="h-4 w-4" />
                        </Button>
                    </div>
                </div>

                <div class="mt-8 pt-6 border-t border-slate-800 flex justify-between items-center">
                    <p class="text-[9px] font-black text-slate-600 uppercase tracking-widest">Authorization required for modification</p>
                    <Button variant="outline" @click="showMembersModal = false" class="rounded-xl border-slate-800 text-[10px] font-black uppercase tracking-widest h-10">
                        Close
                    </Button>
                </div>
            </div>
        </DialogContent>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { authClient } from "~/utils/auth-client"
import { PlusIcon, UsersIcon, Trash2Icon } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const loading = ref(true)
const creating = ref(false)
const showCreateModal = ref(false)
const showMembersModal = ref(false)
const orgs = ref<any[]>([])
const members = ref<any[]>([])
const loadingMembers = ref(false)
const selectedOrg = ref<any>(null)
const editingOrg = ref<any>(null)

const newOrgName = ref('')
const newOrgSlug = ref('')

async function fetchOrgs() {
    loading.value = true
    try {
        console.log("Fetching organizations from authClient...");
        const res = await authClient.organization.list()
        console.log("Organizations response:", res);
        
        if (res && res.data) {
            orgs.value = res.data
        } else if (Array.isArray(res)) {
            orgs.value = res
        }
        console.log("Rendered organizations count:", orgs.value.length);
    } catch (e) {
        console.error("Error fetching organizations:", e)
    } finally {
        loading.value = false
    }
}

async function handleSubmitOrg() {
    creating.value = true
    try {
        if (editingOrg.value) {
            const { error } = await authClient.organization.update({
                organizationId: editingOrg.value.id,
                data: {
                    name: newOrgName.value
                }
            })
            if (error) alert(error.message)
        } else {
            const { error } = await authClient.organization.create({
                name: newOrgName.value,
                slug: newOrgSlug.value
            })
            if (error) alert(error.message)
        }
        
        showCreateModal.value = false
        newOrgName.value = ''
        newOrgSlug.value = ''
        editingOrg.value = null
        await fetchOrgs()
    } catch (e) {
        alert("Operation failed")
    } finally {
        creating.value = false
    }
}

async function handleManageMembers(org: any) {
    selectedOrg.value = org
    showMembersModal.value = true
    loadingMembers.value = true
    try {
        const { data, error } = await authClient.organization.listMembers({
            organizationId: org.id
        })
        if (data) members.value = data
    } catch (e) {
        console.error(e)
    } finally {
        loadingMembers.value = false
    }
}

function handleEditOrg(org: any) {
    editingOrg.value = org
    newOrgName.value = org.name
    newOrgSlug.value = org.slug
    showCreateModal.value = true
}

watch(newOrgName, (val) => {
    if (!editingOrg.value) {
        newOrgSlug.value = val.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
    }
})

watch(showCreateModal, (val) => {
    if (!val) {
        editingOrg.value = null
        newOrgName.value = ''
        newOrgSlug.value = ''
    }
})

onMounted(() => {
    fetchOrgs()
})
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #1e293b;
  border-radius: 10px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #334155;
}
</style>
