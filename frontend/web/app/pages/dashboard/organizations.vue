<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { authClient } from '~/utils/auth-client'
import { Plus, Users, Trash2, Building2 } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import RbacButton from '~/components/common/RbacButton.vue'
import { Input } from '~/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import DashboardOrgCard from '~/components/dashboard/OrgCard.vue'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { canAdministerSystem } = useRbacPermission()

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

// Invite state
const inviteEmail = ref('')
const inviteRole = ref<'member' | 'admin' | 'owner'>('member')
const inviting = ref(false)

// Remove state
const removingMemberId = ref<string | null>(null)

async function fetchOrgs() {
  loading.value = true
  try {
    const res = await $fetch<{ success: boolean; organizations: any[] }>('/api/organizations')
    if (res && res.success && res.organizations && res.organizations.length > 0) {
      orgs.value = res.organizations
    } else {
      await $fetch('/api/dev/seed-admin')
      const seededRes = await $fetch<{ success: boolean; organizations: any[] }>('/api/organizations')
      if (seededRes && seededRes.organizations) {
        orgs.value = seededRes.organizations
      }
    }
  } catch (e) {
    console.error('Error fetching organizations:', e)
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
      if (error) { alert(error.message); return }
    } else {
      const { error } = await authClient.organization.create({
        name: newOrgName.value,
        slug: newOrgSlug.value
      })
      if (error) { alert(error.message); return }
    }

    showCreateModal.value = false
    newOrgName.value = ''
    newOrgSlug.value = ''
    editingOrg.value = null
    await fetchOrgs()
  } catch (e) {
    alert('Operation failed')
  } finally {
    creating.value = false
  }
}

async function handleDeleteOrg(org: any) {
  if (!confirm(`Permanently delete organization "${org.name}"? This cannot be undone.`)) return
  try {
    const { error } = await authClient.organization.delete({ organizationId: org.id })
    if (error) { alert(error.message); return }
    await fetchOrgs()
  } catch (e) {
    alert('Delete failed')
  }
}

async function handleManageMembers(org: any) {
  selectedOrg.value = org
  showMembersModal.value = true
  inviteEmail.value = ''
  inviteRole.value = 'member'
  loadingMembers.value = true
  try {
    const res = await $fetch<{ success: boolean; members: any[] }>(`/api/organizations/${org.id}/members`)
    if (res && res.members) {
      members.value = res.members
    }
  } catch (e) {
    console.error('Error fetching org members:', e)
  } finally {
    loadingMembers.value = false
  }
}

async function handleInviteMember() {
  if (!selectedOrg.value || !inviteEmail.value) return
  inviting.value = true
  try {
    const { error } = await authClient.organization.inviteMember({
      organizationId: selectedOrg.value.id,
      email: inviteEmail.value,
      role: inviteRole.value
    })
    if (error) { alert(error.message); return }
    inviteEmail.value = ''
    // Refresh member list
    const res = await $fetch<{ success: boolean; members: any[] }>(`/api/organizations/${selectedOrg.value.id}/members`)
    if (res?.members) members.value = res.members
    // Refresh org list for updated member count
    await fetchOrgs()
  } catch (e) {
    alert('Invite failed')
  } finally {
    inviting.value = false
  }
}

async function handleRemoveMember(mem: any) {
  if (!selectedOrg.value) return
  if (!confirm(`Remove ${mem.user.name} from ${selectedOrg.value.name}?`)) return
  removingMemberId.value = mem.id
  try {
    const { error } = await (authClient.organization as any).removeMember({
      organizationId: selectedOrg.value.id,
      memberIdOrEmail: mem.user.email
    })
    if (error) { alert(error.message); return }
    members.value = members.value.filter(m => m.id !== mem.id)
    // Refresh org list for updated member count
    await fetchOrgs()
  } catch (e) {
    alert('Remove failed')
  } finally {
    removingMemberId.value = null
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

<template>
  <div class="space-y-6">
    <!-- Header Area -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
          <Building2 class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-100">
            Plant Organizations & Multi-Tenant Boundaries
          </h1>
          <p class="text-sm text-slate-400 mt-0.5">
            Multi-tenant plant isolation, site governance, and member assignment
          </p>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <RbacButton
          capability="canAdministerSystem"
          @click="showCreateModal = true"
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg px-3.5 h-8 text-xs font-medium shadow-sm transition-colors border-0"
        >
          <Plus class="size-3.5 mr-1.5" />
          <span>Create Organization</span>
        </RbacButton>
      </div>
    </div>

    <!-- Organizations Grid -->
    <div v-if="loading && orgs.length === 0" class="flex flex-col items-center justify-center py-24 gap-3">
      <div class="animate-spin rounded-full size-8 border-b-2 border-indigo-500"></div>
      <p class="text-xs font-medium text-slate-400">Loading organizations...</p>
    </div>

    <div v-else-if="orgs.length === 0" class="bg-slate-900 border border-dashed border-slate-800 rounded-xl p-16 text-center shadow-sm">
      <div class="size-14 bg-slate-950 rounded-xl flex items-center justify-center mx-auto mb-3 border border-slate-800 text-slate-600">
        <Building2 class="size-7" />
      </div>
      <h4 class="text-base font-bold text-slate-100">No Organizations Configured</h4>
      <p class="text-slate-400 mt-1 max-w-sm mx-auto text-xs">Configure your first organization to establish secure operational boundaries.</p>
      <Button
        @click="showCreateModal = true"
        class="mt-5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg px-4 h-8 font-medium text-xs shadow-sm transition-colors"
      >
        Create Organization
      </Button>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
      <DashboardOrgCard
        v-for="org in orgs"
        :key="org.id"
        :org="org"
        @manage-members="handleManageMembers"
        @edit="handleEditOrg"
        @delete="handleDeleteOrg"
      />
    </div>

    <!-- Create/Edit Org Modal -->
    <Dialog :open="showCreateModal" @update:open="(val) => !val && (showCreateModal = false)">
      <DialogContent class="max-w-md bg-slate-900 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-xl shadow-2xl">
        <DialogHeader class="p-6 border-b border-slate-800">
          <DialogTitle class="text-base font-bold text-slate-100 flex items-center gap-2.5">
            <div class="p-2 bg-indigo-500/10 border border-indigo-500/20 rounded-lg text-indigo-400">
              <Building2 class="size-4" />
            </div>
            {{ editingOrg ? 'Edit Organization' : 'Create Organization' }}
          </DialogTitle>
          <DialogDescription class="text-xs text-slate-400 mt-1">
            Define operational boundaries and plant identity.
          </DialogDescription>
        </DialogHeader>

        <form @submit.prevent="handleSubmitOrg" class="p-6 space-y-4">
          <div class="space-y-1.5">
            <label class="text-xs font-medium text-slate-400">Organization Name</label>
            <Input
              v-model="newOrgName"
              required
              placeholder="e.g. Assembly Line Operations"
              class="rounded-lg h-9 border-slate-800 bg-slate-950 text-slate-200 text-xs"
            />
          </div>
          <div class="space-y-1.5">
            <label class="text-xs font-medium text-slate-400">System Slug (Generated)</label>
            <Input
              v-model="newOrgSlug"
              disabled
              class="rounded-lg h-9 border-slate-800 bg-slate-950/60 text-slate-500 font-mono text-xs cursor-not-allowed"
            />
          </div>

          <div class="flex justify-end gap-2.5 pt-3 border-t border-slate-800">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              @click="showCreateModal = false"
              class="rounded-lg h-8 text-xs font-medium text-slate-400 hover:text-slate-200"
            >
              Cancel
            </Button>
            <Button
              type="submit"
              size="sm"
              :disabled="creating"
              class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg h-8 text-xs font-medium px-4 shadow-sm transition-colors"
            >
              {{ creating ? 'Saving...' : (editingOrg ? 'Update Organization' : 'Create Organization') }}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>

    <!-- Manage Members Modal -->
    <Dialog :open="showMembersModal" @update:open="(val) => !val && (showMembersModal = false)">
      <DialogContent class="max-w-xl bg-slate-900 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-xl shadow-2xl">
        <DialogHeader class="p-6 border-b border-slate-800">
          <DialogTitle class="text-base font-bold text-slate-100 flex items-center gap-2.5">
            <div class="p-2 bg-indigo-500/10 border border-indigo-500/20 rounded-lg text-indigo-400">
              <Users class="size-4" />
            </div>
            Organization Members
          </DialogTitle>
          <DialogDescription class="text-xs text-slate-400 mt-1">
            Active members assigned to: {{ selectedOrg?.name }}
          </DialogDescription>
        </DialogHeader>

        <div class="p-6 space-y-4">
          <div v-if="loadingMembers" class="flex flex-col items-center justify-center py-10 gap-2.5">
            <div class="size-6 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin"></div>
            <span class="text-xs font-medium text-slate-400">Retrieving roster...</span>
          </div>
          <div v-else class="space-y-2.5 max-h-[260px] overflow-y-auto pr-1">
            <div v-if="members.length === 0" class="text-center py-8 text-slate-500 text-xs font-medium">
              No members assigned to this organization.
            </div>
            <div
              v-for="mem in members"
              :key="mem.id"
              class="flex items-center justify-between p-3 bg-slate-950 border border-slate-800 rounded-lg group hover:border-slate-700 transition-colors"
            >
              <div class="flex items-center gap-3">
                <div class="size-8 rounded-lg bg-slate-800 flex items-center justify-center text-xs font-bold text-slate-300">
                  {{ mem.user.name.charAt(0) }}
                </div>
                <div>
                  <p class="text-xs font-semibold text-slate-200">{{ mem.user.name }}</p>
                  <p class="text-[11px] text-slate-400 font-medium capitalize">{{ mem.role }}</p>
                </div>
              </div>
              <Button
                variant="ghost"
                size="icon"
                class="size-7 text-slate-500 hover:text-rose-400 hover:bg-rose-500/10 rounded-md transition-colors"
                :disabled="removingMemberId === mem.id"
                @click="handleRemoveMember(mem)"
                title="Remove Member"
              >
                <Trash2 v-if="removingMemberId !== mem.id" class="size-3.5" />
                <div v-else class="size-3.5 border-2 border-rose-500 border-t-transparent rounded-full animate-spin" />
              </Button>
            </div>
          </div>

          <!-- Invite Member Form -->
          <div class="pt-4 border-t border-slate-800 space-y-2.5">
            <p class="text-xs font-medium text-slate-400">Invite Member by Email</p>
            <div class="flex gap-2">
              <Input
                v-model="inviteEmail"
                type="email"
                placeholder="colleague@plant.org"
                class="flex-1 rounded-lg h-8 border-slate-800 bg-slate-950 text-slate-200 text-xs"
              />
              <select
                v-model="inviteRole"
                class="bg-slate-950 border border-slate-800 rounded-lg px-2.5 text-xs text-slate-300 h-8 focus:outline-none focus:ring-1 focus:ring-indigo-500 font-medium"
              >
                <option value="member">Member</option>
                <option value="admin">Admin</option>
                <option value="owner">Owner</option>
              </select>
              <Button
                size="sm"
                @click="handleInviteMember"
                :disabled="inviting || !inviteEmail"
                class="h-8 px-3 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium disabled:opacity-50 transition-colors"
              >
                {{ inviting ? '...' : 'Invite' }}
              </Button>
            </div>
          </div>

          <div class="pt-3 border-t border-slate-800 flex justify-end">
            <Button
              variant="outline"
              size="sm"
              @click="showMembersModal = false"
              class="rounded-lg border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 text-xs font-medium h-8 px-3.5 transition-colors"
            >
              Close
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
