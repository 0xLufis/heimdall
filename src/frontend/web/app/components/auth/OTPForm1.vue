<script lang="ts" setup>
const value = ref<string[]>([])
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle>{{ $t('auth.enterVerificationCode') }}</CardTitle>
      <CardDescription>{{ $t('auth.sentVerificationCode') }}</CardDescription>
    </CardHeader>
    <CardContent>
      <form>
        <FieldGroup>
          <Field>
            <FieldLabel html-for="otp">
              {{ $t('auth.verificationCode') }}
            </FieldLabel>
            <PinInput
              id="pin-input"
              v-model="value"
            >
              <PinInputGroup class="gap-2.5 *:data-[slot=pin-input-slot]:rounded-md *:data-[slot=pin-input-slot]:border">
                <PinInputSlot
                  v-for="(id, index) in 6"
                  :key="id"
                  :index="index"
                />
              </PinInputGroup>
            </PinInput>
            <FieldDescription>
              {{ $t('auth.sentVerificationCode') }}
            </FieldDescription>
          </Field>
          <FieldGroup>
            <Button type="submit">
              {{ $t('auth.verify') }}
            </Button>
            <FieldDescription class="text-center">
              {{ $t('auth.didntReceiveCode') }} <a href="#">{{ $t('auth.resend') }}</a>
            </FieldDescription>
          </FieldGroup>
        </FieldGroup>
      </form>
    </CardContent>
  </Card>
</template>

<style>

</style>
