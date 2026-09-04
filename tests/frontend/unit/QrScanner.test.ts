import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import QrScanner from '~/components/ui/qr-scanner/QrScanner.vue'

describe('Camera QR Code Scanner Component', () => {
  it('renders QrScanner title and manual input fallback', () => {
    const wrapper = mount(QrScanner, {
      props: {
        open: true,
        title: 'Scan Equipment QR Code'
      }
    })

    expect(wrapper.text()).toContain('Scan Equipment QR Code')
    expect(wrapper.text()).toContain('Manual Equipment ID Entry')
    expect(wrapper.find('video').exists()).toBe(true)
  })

  it('emits scanned event when manual input is submitted', async () => {
    const wrapper = mount(QrScanner, {
      props: { open: true }
    })

    const input = wrapper.find('input')
    await input.setValue('STATION-OP10-9823')

    const useIdBtn = wrapper.findAll('button').find(b => b.text().includes('Use ID'))
    expect(useIdBtn).toBeDefined()
    await useIdBtn!.trigger('click')

    expect(wrapper.emitted('scanned')).toBeTruthy()
    expect(wrapper.emitted('scanned')![0]).toEqual(['STATION-OP10-9823'])
  })

  it('emits close event when close button is clicked', async () => {
    const wrapper = mount(QrScanner, {
      props: { open: true }
    })

    const closeBtn = wrapper.find('button[title="Close Scanner"]')
    await closeBtn.trigger('click')

    expect(wrapper.emitted('close')).toBeTruthy()
  })
})
