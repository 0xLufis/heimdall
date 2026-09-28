<script setup lang="ts">
import { ref, watch } from 'vue'
import { authClient } from '~/utils/auth-client'
import { useAuthSession } from '~/composables/useAuthSession'
import RoleBadge from '~/components/dashboard/RoleBadge.vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Avatar, AvatarImage, AvatarFallback } from '@/components/ui/avatar'
import {
  CheckIcon,
  AlertCircleIcon,
  RefreshCwIcon,
  CheckCircle2Icon
} from 'lucide-vue-next'

const { user, userRole } = useAuthSession()

const profileName = ref('')
const profileEmail = ref('')
const profileAvatar = ref('')
const profileSuccess = ref('')
const profileError = ref('')
const savingProfile = ref(false)

const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const passwordSuccess = ref('')
const passwordError = ref('')
const changingPassword = ref(false)

function syncProfileFields() {
  if (user.value) {
    profileName.value = user.value.name || ''
    profileEmail.value = user.value.email || ''
    profileAvatar.value = (user.value as any)?.image || (user.value as any)?.avatar || ''
  }
}

watch(() => user.value, () => {
  syncProfileFields()
}, { immediate: true })

async function handleSaveProfile() {
  savingProfile.value = true
  profileSuccess.value = ''
  profileError.value = ''
  try {
    if (authClient && (authClient as any).updateUser) {
      await (authClient as any).updateUser({
        name: profileName.value,
        image: profileAvatar.value
      })
    }
    if (user.value) {
      user.value.name = profileName.value
      if (profileAvatar.value) {
        (user.value as any).image = profileAvatar.value
      }
    }
    profileSuccess.value = 'Profile information saved successfully.'
  } catch (err: any) {
    profileError.value = err.message || 'Failed to update profile.'
  } finally {
    savingProfile.value = false
  }
}

async function handleChangePassword() {
  if (!currentPassword.value || !newPassword.value) {
    passwordError.value = 'Please provide both current and new passwords.'
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    passwordError.value = 'New password and confirmation do not match.'
    return
  }
  if (newPassword.value.length < 8) {
    passwordError.value = 'Password must be at least 8 characters in length.'
    return
  }

  changingPassword.value = true
  passwordSuccess.value = ''
  passwordError.value = ''

  try {
    if (authClient && (authClient as any).changePassword) {
      const { error: changeErr } = await (authClient as any).changePassword({
        currentPassword: currentPassword.value,
        newPassword: newPassword.value,
        revokeOtherSessions: true
      })
      if (changeErr) {
        passwordError.value = changeErr.message || 'Failed to update password.'
        return
      }
    }
    passwordSuccess.value = 'Password successfully updated and other sessions revoked.'
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
  } catch (err: any) {
    passwordError.value = err.message || 'Failed to update password.'
  } finally {
    changingPassword.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Personal Identity -->
    <Card class="border-border/80">
      <CardHeader>
        <CardTitle class="text-lg">Personal Identity &amp; Profile</CardTitle>
        <CardDescription>
          Your public display information across Heimdall, ticket comments, and audit logs.
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-6">
        <div v-if="profileSuccess" class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
          <CheckCircle2Icon class="h-4 w-4 shrink-0" />
          <span>{{ profileSuccess }}</span>
        </div>
        <div v-if="profileError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
          <AlertCircleIcon class="h-4 w-4 shrink-0" />
          <span>{{ profileError }}</span>
        </div>

        <div class="flex flex-col sm:flex-row items-center gap-6 p-4 rounded-lg bg-muted/30 border border-border/50">
          <Avatar class="h-20 w-20 rounded-xl border-2 border-primary/40 shadow-md">
            <AvatarImage :src="profileAvatar" :alt="profileName" />
            <AvatarFallback class="rounded-xl bg-primary text-primary-foreground font-black text-2xl">
              {{ profileName?.charAt(0).toUpperCase() || 'U' }}
            </AvatarFallback>
          </Avatar>
          <div class="space-y-1 text-center sm:text-left">
            <div class="text-lg font-bold">{{ profileName || 'Unnamed User' }}</div>
            <div class="text-xs text-muted-foreground font-mono">{{ profileEmail }}</div>
            <div class="pt-2 flex flex-wrap gap-2 justify-center sm:justify-start">
              <RoleBadge :role="userRole" />
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-muted-foreground uppercase">Full Name</label>
            <Input v-model="profileName" placeholder="e.g. Technician Name" />
          </div>
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-muted-foreground uppercase">Email Address</label>
            <Input v-model="profileEmail" placeholder="user@heimdall.dev" :disabled="true" class="bg-muted/50 font-mono text-xs" />
            <p class="text-[10px] text-muted-foreground">Contact IT Administrator to change verified email.</p>
          </div>
          <div class="space-y-1.5 md:col-span-2">
            <label class="text-xs font-semibold text-muted-foreground uppercase">Avatar Image URL</label>
            <Input v-model="profileAvatar" placeholder="https://example.com/avatar.png" />
          </div>
        </div>
      </CardContent>
      <CardFooter class="flex justify-end gap-2 border-t border-border/50 pt-4">
        <Button size="sm" @click="handleSaveProfile" :disabled="savingProfile">
          <RefreshCwIcon v-if="savingProfile" class="h-3.5 w-3.5 mr-2 animate-spin" />
          <CheckIcon v-else class="h-3.5 w-3.5 mr-2" />
          Save Profile Changes
        </Button>
      </CardFooter>
    </Card>

    <!-- Password Management -->
    <Card class="border-border/80">
      <CardHeader>
        <CardTitle class="text-lg">Security &amp; Password</CardTitle>
        <CardDescription>
          Update your local authentication password. Requires current password verification.
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-4">
        <div v-if="passwordSuccess" class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
          <CheckCircle2Icon class="h-4 w-4 shrink-0" />
          <span>{{ passwordSuccess }}</span>
        </div>
        <div v-if="passwordError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
          <AlertCircleIcon class="h-4 w-4 shrink-0" />
          <span>{{ passwordError }}</span>
        </div>

        <div class="space-y-3 max-w-md">
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-muted-foreground uppercase">Current Password</label>
            <Input v-model="currentPassword" type="password" placeholder="••••••••••••" />
          </div>
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-muted-foreground uppercase">New Password</label>
            <Input v-model="newPassword" type="password" placeholder="••••••••••••" />
          </div>
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-muted-foreground uppercase">Confirm New Password</label>
            <Input v-model="confirmPassword" type="password" placeholder="••••••••••••" />
          </div>
        </div>
      </CardContent>
      <CardFooter class="flex justify-end border-t border-border/50 pt-4">
        <Button size="sm" @click="handleChangePassword" :disabled="changingPassword">
          <RefreshCwIcon v-if="changingPassword" class="h-3.5 w-3.5 mr-2 animate-spin" />
          Change Password
        </Button>
      </CardFooter>
    </Card>
  </div>
</template>
