<script setup lang="ts">
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { HardDrive, Cpu, Package, Settings, ShieldAlert, FileSearch, X } from 'lucide-vue-next'
import { authClient } from '@/utils/auth-client'

const props = defineProps<{
  client: any
  isOpen: boolean
}>()

const emit = defineEmits(['close'])

const authSession = authClient.useSession()
const isAdmin = computed(() => authSession?.data?.value?.user?.role === 'admin')

const hardware = computed(() => {
  const hw = props.client.inventoryItems?.find((c: any) => c.name === 'Hardware')?.metadata
  if (!hw) return {}
  // Normalize keys to lowercase for easier template access
  return Object.keys(hw).reduce((acc: any, key) => {
    acc[key.toLowerCase()] = hw[key]
    return acc
  }, {})
})

const software = computed(() => {
  const sw = props.client.inventoryItems?.find((c: any) => c.name === 'Software')?.metadata
  if (!sw) return { installedpackages: [] }
  return Object.keys(sw).reduce((acc: any, key) => {
    acc[key.toLowerCase()] = sw[key]
    return acc
  }, {})
})

const physicalDrives = computed(() => {
  const drives = props.client.inventoryItems?.find((c: any) => c.name === 'PhysicalDrives')?.metadata
  if (!Array.isArray(drives)) return []
  return drives.map((d: any) => {
    return Object.keys(d).reduce((acc: any, key) => {
      acc[key.toLowerCase()] = d[key]
      return acc
    }, {})
  })
})

const configUpdate = ref({
  backendUrl: '',
  authType: 'NoAuth'
})

const fileCheckPath = ref('')

const sendConfigUpdate = async () => {
  try {
    await $fetch(`/api/proxy/AgentCommand/${props.client.id}/update-config`, {
      method: 'POST',
      body: {
        config: configUpdate.value
      }
    })
    alert('Config update queued for admin approval/agent sync')
  } catch (e) {
    console.error(e)
    alert('Failed to queue config update')
  }
}

const sendFileCheck = async () => {
  try {
    await $fetch(`/api/proxy/AgentCommand/${props.client.id}/file-check`, {
      method: 'POST',
      body: JSON.stringify(fileCheckPath.value),
      headers: { 'Content-Type': 'application/json' }
    })
    alert('File check queued')
  } catch (e) {
    console.error(e)
    alert('Failed to queue file check')
  }
}

watch(() => props.client, (newClient) => {
  if (newClient) {
    // Optionally pre-fill config update from current metadata if available
  }
}, { immediate: true })

</script>

<template>
  <Dialog :open="isOpen" @update:open="$emit('close')">
    <DialogContent :show-close="false" class="max-w-4xl max-h-[90vh] overflow-hidden flex flex-col bg-card border-border text-foreground rounded-3xl p-0 shadow-2xl">
      <DialogHeader class="p-8 border-b border-border shrink-0 flex flex-row items-center justify-between">
        <DialogTitle class="text-2xl font-black uppercase tracking-tight flex items-center gap-3 text-foreground">
          <div class="w-2 h-6 bg-indigo-500 rounded-full"></div>
          {{ client.hostname }}
          <span v-if="client.lastSeen" class="text-[10px] font-bold text-muted-foreground ml-2 uppercase tracking-widest">
            Last seen: {{ new Date(client.lastSeen).toLocaleString() }}
          </span>
        </DialogTitle>
        <button
          type="button"
          @click="$emit('close')"
          class="p-2 rounded-xl text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
          title="Close details"
        >
          <X class="w-5 h-5" />
        </button>
      </DialogHeader>

      <div class="flex-grow overflow-y-auto p-8">
        <Tabs defaultValue="hardware" class="w-full">
          <TabsList class="bg-muted border border-border p-1 rounded-2xl mb-8">
            <TabsTrigger value="hardware" class="rounded-xl data-[state=active]:bg-card data-[state=active]:text-foreground data-[state=active]:shadow-sm text-muted-foreground uppercase text-[10px] font-black tracking-widest px-6 transition-all">
              <Cpu class="w-3 h-3 mr-2" /> Hardware
            </TabsTrigger>
            <TabsTrigger value="storage" class="rounded-xl data-[state=active]:bg-card data-[state=active]:text-foreground data-[state=active]:shadow-sm text-muted-foreground uppercase text-[10px] font-black tracking-widest px-6 transition-all">
              <HardDrive class="w-3 h-3 mr-2" /> Storage
            </TabsTrigger>
            <TabsTrigger value="software" class="rounded-xl data-[state=active]:bg-card data-[state=active]:text-foreground data-[state=active]:shadow-sm text-muted-foreground uppercase text-[10px] font-black tracking-widest px-6 transition-all">
              <Package class="w-3 h-3 mr-2" /> Software
            </TabsTrigger>
            <TabsTrigger v-if="isAdmin" value="admin" class="rounded-xl data-[state=active]:bg-rose-600 data-[state=active]:text-white text-muted-foreground uppercase text-[10px] font-black tracking-widest px-6 transition-all">
              <Settings class="w-3 h-3 mr-2" /> Management
            </TabsTrigger>
          </TabsList>

          <TabsContent value="hardware" class="space-y-6">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Card class="bg-card border-border rounded-2xl text-foreground">
                <CardContent class="p-6">
                  <Label class="text-[10px] uppercase tracking-widest text-muted-foreground font-black">Processor</Label>
                  <p class="text-lg font-bold mt-1">{{ hardware.cpu || 'Unknown' }}</p>
                </CardContent>
              </Card>
              <Card class="bg-card border-border rounded-2xl text-foreground">
                <CardContent class="p-6">
                  <Label class="text-[10px] uppercase tracking-widest text-muted-foreground font-black">Memory</Label>
                  <p class="text-lg font-bold mt-1">{{ hardware.ram || 'Unknown' }}</p>
                </CardContent>
              </Card>
              <Card class="bg-card border-border rounded-2xl col-span-full text-foreground">
                <CardContent class="p-6">
                  <Label class="text-[10px] uppercase tracking-widest text-muted-foreground font-black">Motherboard</Label>
                  <p class="text-lg font-bold mt-1">{{ hardware.motherboard || 'Unknown' }}</p>
                </CardContent>
              </Card>
            </div>
          </TabsContent>

          <TabsContent value="storage" class="space-y-6">
            <div v-for="drive in physicalDrives" :key="drive.serialnumber" class="bg-card border border-border p-6 rounded-2xl flex items-center justify-between text-foreground">
              <div>
                <h4 class="font-bold text-foreground">{{ drive.model }}</h4>
                <p class="text-[10px] font-mono text-muted-foreground">SN: {{ drive.serialnumber }} | {{ drive.interfacetype }}</p>
              </div>
              <div class="text-right">
                <span class="text-lg font-black text-indigo-600 dark:text-indigo-400">{{ (drive.sizebytes / (1024**3)).toFixed(0) }} GB</span>
              </div>
            </div>
            <div v-if="physicalDrives.length === 0" class="text-center py-12 text-muted-foreground">
               No physical drive data reported.
            </div>
          </TabsContent>

          <TabsContent value="software" class="space-y-4">
            <div class="bg-card border border-border rounded-2xl p-6 text-foreground">
               <Label class="text-[10px] uppercase tracking-widest text-muted-foreground font-black mb-4 block">Installed Packages ({{ software.installedpackages?.length || 0 }})</Label>
               <div class="grid grid-cols-1 md:grid-cols-2 gap-2">
                  <div v-for="pkg in software.installedpackages" :key="pkg" class="text-xs py-2 px-3 bg-muted/40 border border-border rounded-lg text-foreground font-mono">
                    {{ pkg }}
                  </div>
               </div>
            </div>
          </TabsContent>

          <TabsContent v-if="isAdmin" value="admin" class="space-y-8 animate-in slide-in-from-bottom-2 duration-300">
             <div class="bg-rose-500/10 border border-rose-500/30 p-6 rounded-2xl flex items-start gap-4">
                <ShieldAlert class="w-6 h-6 text-rose-500 shrink-0" />
                <div>
                  <h4 class="text-sm font-black text-rose-700 dark:text-rose-400 uppercase tracking-widest">Admin Management</h4>
                  <p class="text-[10px] text-muted-foreground mt-1">These actions will be queued and sent to the agent during its next sync cycle (approx. 5-30 seconds).</p>
                </div>
             </div>

             <div class="grid grid-cols-1 gap-8">
                <div class="space-y-4">
                  <h5 class="text-xs font-black uppercase tracking-widest text-foreground flex items-center gap-2">
                    <Settings class="w-4 h-4" /> Auth & Connection Update
                  </h5>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div class="space-y-2">
                      <Label class="text-[10px] uppercase font-bold text-muted-foreground">Backend URL</Label>
                      <Input v-model="configUpdate.backendUrl" placeholder="https://heimdall.example.com" class="bg-background border-border text-foreground" />
                    </div>
                    <div class="space-y-2">
                      <Label class="text-[10px] uppercase font-bold text-muted-foreground">Auth Method</Label>
                      <select v-model="configUpdate.authType" class="w-full h-10 px-3 bg-background border border-border rounded-md text-sm text-foreground">
                         <option value="NoAuth">No Auth</option>
                         <option value="HeimdallCert">Heimdall Certificate</option>
                         <option value="UserCert">User/AD Certificate</option>
                      </select>
                    </div>
                  </div>
                  <Button @click="sendConfigUpdate" variant="destructive" class="w-full bg-rose-600 hover:bg-rose-700 text-white text-[10px] font-black uppercase tracking-widest h-12 rounded-xl mt-2">
                    Queue Configuration Update
                  </Button>
                </div>

                <div class="pt-8 border-t border-border space-y-4">
                  <h5 class="text-xs font-black uppercase tracking-widest text-foreground flex items-center gap-2">
                    <FileSearch class="w-4 h-4" /> Diagnostic File Check
                  </h5>
                  <div class="space-y-2">
                    <Label class="text-[10px] uppercase font-bold text-muted-foreground">Full File Path</Label>
                    <Input v-model="fileCheckPath" placeholder="C:\Windows\System32\drivers\etc\hosts" class="bg-background border-border text-foreground" />
                  </div>
                  <Button @click="sendFileCheck" variant="outline" class="w-full border-border bg-card text-foreground hover:bg-muted text-[10px] font-black uppercase tracking-widest h-12 rounded-xl">
                    Request File Presence Check
                  </Button>
                </div>
             </div>
          </TabsContent>
        </Tabs>
      </div>
    </DialogContent>
  </Dialog>
</template>
