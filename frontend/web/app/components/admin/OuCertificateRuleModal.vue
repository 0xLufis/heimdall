<script setup lang="ts">
import { ref, watch } from 'vue'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { KeyRoundIcon, NetworkIcon, CheckIcon } from 'lucide-vue-next'

const props = defineProps<{
  open: boolean
  ruleToEdit?: {
    id?: string
    ouPath: string
    profileName: string
    validityYears: number
    autoEnroll: boolean
    keyAlgorithm: string
  } | null
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', rule: any): void
}>()

const ouPath = ref('')
const profileName = ref('')
const validityYears = ref(2)
const autoEnroll = ref(true)
const keyAlgorithm = ref('RSA-2048')
const saving = ref(false)
const errorMessage = ref('')

watch(
  () => props.ruleToEdit,
  (val) => {
    if (val) {
      ouPath.value = val.ouPath || ''
      profileName.value = val.profileName || ''
      validityYears.value = val.validityYears || 2
      autoEnroll.value = val.autoEnroll !== false
      keyAlgorithm.value = val.keyAlgorithm || 'RSA-2048'
    } else {
      ouPath.value = ''
      profileName.value = ''
      validityYears.value = 2
      autoEnroll.value = true
      keyAlgorithm.value = 'RSA-2048'
    }
  },
  { immediate: true },
)

const sampleOus = [
  'OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp',
  'OU=Fastening,OU=VLAN50-Joining,DC=factory,DC=corp',
  'OU=AOI-Vision,OU=VLAN20-Inspection,DC=factory,DC=corp',
  'OU=Milling,OU=VLAN30-Machining,DC=factory,DC=corp',
  'OU=Dispensing,OU=VLAN40-Chemical,DC=factory,DC=corp',
]

function selectSampleOu(path: string) {
  ouPath.value = path
  if (!profileName.value) {
    const ouName = path.split(',')[0].replace('OU=', '')
    profileName.value = `${ouName}-mTLS-Profile`
  }
}

async function saveRule() {
  if (!ouPath.value.trim() || !profileName.value.trim()) {
    errorMessage.value = 'Both OU Path and Profile Name are required.'
    return
  }

  saving.value = true
  errorMessage.value = ''

  try {
    const payload = {
      id: props.ruleToEdit?.id,
      ouPath: ouPath.value.trim(),
      profileName: profileName.value.trim(),
      validityYears: validityYears.value,
      autoEnroll: autoEnroll.value,
      keyAlgorithm: keyAlgorithm.value,
    }

    let result: any = null
    try {
      result = await $fetch('/api/proxy/v1/certificatemanagement/ou-rules', {
        method: 'POST',
        body: payload,
      })
    } catch {
      result = await $fetch('/api/pki/ou-rules', {
        method: 'POST',
        body: payload,
      })
    }

    emit('saved', result)
    emit('close')
  } catch (err: any) {
    errorMessage.value = err?.data?.message || err?.message || 'Failed to save OU rule.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="(val: boolean) => { if (!val) emit('close') }">
    <DialogContent class="max-w-lg bg-slate-900 border-slate-800 text-slate-100">
      <DialogHeader>
        <DialogTitle class="flex items-center gap-2 text-base text-indigo-400">
          <KeyRoundIcon class="h-5 w-5" />
          {{ ruleToEdit ? 'Edit Active Directory Certificate Enrollment Policy' : 'Configure Active Directory Certificate Enrollment Policy' }}
        </DialogTitle>
        <DialogDescription class="text-xs text-slate-400">
          Automatically enroll and issue mTLS client certificates whenever hosts in this Active Directory OU are discovered.
        </DialogDescription>
      </DialogHeader>

      <div class="space-y-4 py-2">
        <div v-if="errorMessage" class="p-2.5 rounded bg-rose-500/15 border border-rose-500/30 text-rose-300 text-xs">
          {{ errorMessage }}
        </div>

        <div>
          <label class="text-xs font-medium text-slate-300">Active Directory OU Path (Distinguished Name)</label>
          <Input
            v-model="ouPath"
            placeholder="e.g. OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp"
            class="mt-1 bg-slate-950 border-slate-800 text-slate-100 text-xs font-mono"
          />
          <div class="mt-2 flex flex-wrap gap-1.5">
            <button
              v-for="p in sampleOus"
              :key="p"
              type="button"
              @click="selectSampleOu(p)"
              class="text-[10px] px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 transition-colors"
            >
              {{ p.split(',')[0].replace('OU=', '') }} ({{ p.split(',')[1]?.replace('OU=', '') }})
            </button>
          </div>
        </div>

        <div>
          <label class="text-xs font-medium text-slate-300">Certificate Profile Name</label>
          <Input
            v-model="profileName"
            placeholder="e.g. High-Assurance-Robotics-mTLS"
            class="mt-1 bg-slate-950 border-slate-800 text-slate-100 text-sm"
          />
        </div>

        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="text-xs font-medium text-slate-300">Validity (Years)</label>
            <Input
              type="number"
              min="1"
              max="10"
              v-model.number="validityYears"
              class="mt-1 bg-slate-950 border-slate-800 text-slate-100 text-sm"
            />
          </div>

          <div>
            <label class="text-xs font-medium text-slate-300">Cryptographic Algorithm</label>
            <select
              v-model="keyAlgorithm"
              class="mt-1 flex h-9 w-full rounded-md border border-slate-800 bg-slate-950 px-3 py-1 text-xs text-slate-200"
            >
              <option value="RSA-2048">RSA-2048 (Standard)</option>
              <option value="RSA-4096">RSA-4096 (High-Security)</option>
              <option value="ECDSA-P256">ECDSA-P256 (NIST Curve)</option>
              <option value="ECDSA-P384">ECDSA-P384 (Suite B)</option>
            </select>
          </div>
        </div>

        <div class="flex items-center justify-between p-3 rounded-lg border border-slate-800 bg-slate-950/50">
          <div>
            <div class="text-xs font-medium text-slate-200">Auto-Enroll Upon Discovery</div>
            <div class="text-[11px] text-slate-400">Issue certificate automatically when edge host imports from this OU</div>
          </div>
          <input
            type="checkbox"
            v-model="autoEnroll"
            class="h-4 w-4 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500"
          />
        </div>
      </div>

      <DialogFooter class="flex items-center justify-between border-t border-slate-800 pt-3">
        <Button variant="ghost" size="sm" @click="emit('close')" class="text-slate-400">
          Dismiss
        </Button>
        <Button
          size="sm"
          class="bg-indigo-600 hover:bg-indigo-500 text-white"
          :disabled="saving || !ouPath || !profileName"
          @click="saveRule"
        >
          <CheckIcon class="h-4 w-4 mr-1.5" />
          {{ saving ? 'Saving Rule...' : 'Commit Policy Rule' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
