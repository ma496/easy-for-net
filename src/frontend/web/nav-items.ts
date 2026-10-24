import { Users, Shield, Home, User, Lock, Bell, Building2, Layers, Languages, Settings } from 'lucide-react'
// efn:examples:start
import { Palette, Zap, Calendar, TreePine, FormInput, Upload, Clock } from 'lucide-react'
// efn:examples:end
import type { ElementType } from 'react'

/** A grouped section of nav items rendered together under a shared heading in the sidebar/navigation. */
export interface NavItemGroup {
  title: string
  items: NavItem[]
}

/** Describes a single navigation entry, including its i18n title, optional target url, icon, badge, group, visibility, active state, and nested children. An item whose children are all `show: false` renders in the sidebar as a single link that stays active on them; the breadcrumbs still trail through them. An item with no url is a heading for its children only: it is never a link, and it is dropped from the sidebar when none of its children remain to show. */
export interface NavItem {
  title: string
  url?: string
  icon?: ElementType
  badge?: number
  group?: string
  show?: boolean
  isActive?: boolean
  children?: NavItem[]
}

/** Static navigation menu used by the app shell, mixing standalone nav items and grouped sections with i18n-keyed titles. */
export const navItems: (NavItem | NavItemGroup)[] = [
  {
    title: 'navigation.dashboard',
    url: '/',
    icon: Home,
    children: [
      {
        title: 'navigation.dashboardSales',
        url: '/',
      },
    ],
  },
  {
    title: 'navigation.administration',
    items: [
      {
        title: 'navigation.users',
        url: '/users',
        icon: Users,
        children: [
          {
            title: 'navigation.usersList',
            url: '/users',

          },
          {
            title: 'navigation.usersCreate',
            url: '/users/create',

          },
          {
            title: 'navigation.usersUpdate',
            url: '/users/{id}/update',

            show: false,
          },
        ],
      },
      {
        title: 'navigation.roles',
        url: '/roles',
        icon: Shield,
        children: [
          {
            title: 'navigation.rolesList',
            url: '/roles',

          },
          {
            title: 'navigation.rolesCreate',
            url: '/roles/create',

          },
          {
            title: 'navigation.rolesUpdate',
            url: '/roles/{id}/update',

            show: false,
          },
          {
            title: 'navigation.rolesChangePermissions',
            url: '/roles/{id}/change-permissions',

            show: false,
          },
        ],
      },
      {
        title: 'navigation.tenants',
        url: '/tenants',
        icon: Building2,
        children: [
          {
            title: 'navigation.tenantsList',
            url: '/tenants',
          },
          {
            title: 'navigation.tenantsCreate',
            url: '/tenants/create',
          },
          {
            title: 'navigation.tenantsUpdate',
            url: '/tenants/{id}/update',
            show: false,
          },
          {
            title: 'navigation.tenantsMembers',
            url: '/tenants/{id}/members',
            show: false,
          },
          {
            title: 'navigation.tenantsDetail',
            url: '/tenants/{id}/detail',
            show: false,
          },
          {
            title: 'navigation.tenantsFeatures',
            url: '/tenants/{id}/features',
            show: false,
          },
        ],
      },
      {
        title: 'navigation.editions',
        url: '/editions',
        icon: Layers,
        children: [
          {
            title: 'navigation.editionsList',
            url: '/editions',
          },
          {
            title: 'navigation.editionsCreate',
            url: '/editions/create',
          },
          {
            title: 'navigation.editionsUpdate',
            url: '/editions/{id}/update',
            show: false,
          },
          {
            title: 'navigation.editionsFeatures',
            url: '/editions/{id}/features',
            show: false,
          },
        ],
      },
      {
        title: 'navigation.localization',
        url: '/localization',
        icon: Languages,
      },
      {
        title: 'navigation.settings',
        url: '/settings',
        icon: Settings,
      },
      {
        title: 'navigation.notifications',
        url: '/notifications',
        icon: Bell,
        children: [
          {
            title: 'navigation.notificationsDetail',
            url: '/notifications/{id}',
            show: false,
          },
        ],
      },
    ],
  },
  // efn:examples:start
  {
    title: 'navigation.components',
    items: [
      {
        title: 'navigation.uiFormElements',
        url: '/ui/form-elements',
        icon: FormInput,
      },
      {
        title: 'navigation.uiButtons',
        url: '/ui/buttons',
        icon: Zap,
      },
      {
        title: 'navigation.uiCards',
        url: '/ui/cards',
        icon: Palette,
      },
      {
        title: 'navigation.uiDatePicker',
        url: '/ui/date-picker',
        icon: Calendar,
      },
      {
        title: 'navigation.uiDateView',
        url: '/ui/date-view',
        icon: Clock,
      },
      {
        title: 'navigation.uiTreeview',
        url: '/ui/treeview',
        icon: TreePine,
      },
      {
        title: 'navigation.uiFileUpload',
        url: '/ui/file-upload',
        icon: Upload,
      },
      {
        title: 'navigation.uiTooltips',
        url: '/ui/tooltips',
        icon: Palette,
      },
    ],
  },
  // efn:examples:end
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
