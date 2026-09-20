import { Users, Shield, Home, User, Lock, Palette, Zap, Calendar, TreePine, FormInput, Upload, Clock, Bell, Building2 } from 'lucide-react'
import type { ElementType } from 'react'


/** A grouped section of nav items rendered together under a shared heading in the sidebar/navigation. */
export interface NavItemGroup {
  title: string
  items: NavItem[]
}

/** Describes a single navigation entry, including its i18n title, target url, optional icon, badge, group, visibility, active state, and nested children. */
export interface NavItem {
  title: string
  url: string
  icon?: ElementType
  badge?: number
  group?: string
  show?: boolean
  isActive?: boolean
  children?: NavItem[]
}

/** Static navigation menu used by the admin shell, mixing standalone nav items and grouped sections with i18n-keyed titles. */
export const navItems: (NavItem | NavItemGroup)[] = [
  {
    title: 'navigation.dashboard',
    url: '/admin',
    icon: Home,
    children: [
      {
        title: 'navigation.dashboardSales',
        url: '/admin',
      },
    ],
  },
  {
    title: 'navigation.administration',
    items: [
      {
        title: 'navigation.users',
        url: '/admin/users/list',
        icon: Users,
        children: [
          {
            title: 'navigation.usersList',
            url: '/admin/users/list',

          },
          {
            title: 'navigation.usersCreate',
            url: '/admin/users/create',

          },
          {
            title: 'navigation.usersUpdate',
            url: '/admin/users/update/{id}',

            show: false,
          },
        ],
      },
      {
        title: 'navigation.roles',
        url: '/admin/roles/list',
        icon: Shield,
        children: [
          {
            title: 'navigation.rolesList',
            url: '/admin/roles/list',

          },
          {
            title: 'navigation.rolesCreate',
            url: '/admin/roles/create',

          },
          {
            title: 'navigation.rolesUpdate',
            url: '/admin/roles/update/{id}',

            show: false,
          },
          {
            title: 'navigation.rolesChangePermissions',
            url: '/admin/roles/change-permissions/{id}',

            show: false,
          },
        ],
      },
      {
        title: 'navigation.tenants',
        url: '/admin/tenants/list',
        icon: Building2,
        children: [
          {
            title: 'navigation.tenantsList',
            url: '/admin/tenants/list',
          },
          {
            title: 'navigation.tenantsCreate',
            url: '/admin/tenants/create',
          },
          {
            title: 'navigation.tenantsUpdate',
            url: '/admin/tenants/update/{id}',
            show: false,
          },
          {
            title: 'navigation.tenantsMembers',
            url: '/admin/tenants/members/{id}',
            show: false,
          },
          {
            title: 'navigation.tenantsDetail',
            url: '/admin/tenants/detail/{id}',
            show: false,
          },
        ],
      },
      {
        title: 'navigation.notifications',
        url: '/admin/notifications/list',
        icon: Bell,
        children: [
          {
            title: 'navigation.notificationsList',
            url: '/admin/notifications/list',
          },
          {
            title: 'navigation.notificationsDetail',
            url: '/admin/notifications/{id}',
            show: false,
          },
        ],
      },
    ],
  },
  {
    title: 'navigation.components',
    items: [
      {
        title: 'navigation.uiFormElements',
        url: '/admin/ui/form-elements',
        icon: FormInput,
      },
      {
        title: 'navigation.uiButtons',
        url: '/admin/ui/buttons',
        icon: Zap,
      },
      {
        title: 'navigation.uiCards',
        url: '/admin/ui/cards',
        icon: Palette,
      },
      {
        title: 'navigation.uiDatePicker',
        url: '/admin/ui/date-picker',
        icon: Calendar,
      },
      {
        title: 'navigation.uiDateView',
        url: '/admin/ui/date-view',
        icon: Clock,
      },

      {
        title: 'navigation.uiTreeview',
        url: '/admin/ui/treeview',
        icon: TreePine,
      },
      {
        title: 'navigation.uiFileUpload',
        url: '/admin/ui/file-upload',
        icon: Upload,
      },
      {
        title: 'navigation.uiTooltips',
        url: '/admin/ui/tooltips',
        icon: Palette,
      },
    ],
  },
  {
    title: 'navigation.changePassword',
    url: '/change-password',
    icon: Lock,
    show: false,
  },
  {
    title: 'navigation.profile',
    url: '/profile',
    icon: User,
    show: false,
  },
  {
    title: 'navigation.selectTenant',
    url: '/select-tenant',
    show: false,
  },
]
