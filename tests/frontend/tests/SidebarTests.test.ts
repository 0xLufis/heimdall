import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import AppSidebar from '../../app/components/layout/AppSidebar.vue'
import { navMenu, navMenuBottom } from '../../app/constants/menus' // Adjust path as necessary

describe('AppSidebar', () => {
  it('renders correctly in expanded state', async () => {
    const wrapper = mount(AppSidebar, {
      props: {
        // Mock any props if AppSidebar expects them
      },
      global: {
        stubs: {
          // Stub out NuxtLink, Icon, etc. if they cause issues or are external
          NuxtLink: { template: '<a><slot /></a>' },
          Icon: { template: '<span></span>' },
          Sidebar: { template: '<div class="sidebar-expanded"><slot /></div>' },
          SidebarHeader: { template: '<div><slot /></div>' },
          SidebarContent: { template: '<div><slot /></div>' },
          SidebarGroup: { template: '<div><slot /></div>' },
          SidebarGroupLabel: { template: '<div>{{ heading }}</div>', props: ['heading'] },
          SidebarMenuItem: { template: '<div><slot /></div>' },
          SidebarMenuButton: { template: '<button><slot /></button>' },
          LayoutSidebarNavHeader: { template: '<div></div>' },
          Search: { template: '<div></div>' },
          LayoutSidebarNavFooter: { template: '<div></div>' },
        },
        mocks: {
          useAppSettings: () => ({
            sidebar: {
              collapsible: 'none', // Expanded state
              side: 'left',
              variant: 'inset'
            }
          }),
          // Mock authClient.useSession
          authClient: {
            useSession: () => ({
              data: {
                value: {
                  user: {
                    name: 'Test User',
                    email: 'test@example.com',
                    image: '/avatars/default.png'
                  }
                }
              }
            })
          }
        }
      }
    })

    // Check if the sidebar content is rendered
    expect(wrapper.find('.sidebar-expanded').exists()).toBe(true)
    
    // Check if main menu items are rendered
    navMenu.forEach(group => {
      if (group.heading) {
        expect(wrapper.text()).toContain(group.heading)
      }
      group.items.forEach(item => {
        expect(wrapper.text()).toContain(item.title)
      })
    })

    // Check if bottom menu items are rendered
    navMenuBottom.forEach(item => {
      expect(wrapper.text()).toContain(item.title)
    })
  })

  // Add more tests for collapsed state, interaction, etc.
})
