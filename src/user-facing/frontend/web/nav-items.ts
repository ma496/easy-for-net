import { Users, Shield, Home, User, Lock, Bell, Languages, Settings, Search } from 'lucide-react'
// efn:examples:start
import { Palette, Zap, Calendar, TreePine, FormInput, Upload, Clock } from 'lucide-react'
// efn:examples:end
import type { ElementType } from 'react'
import { accountPath, adminPath } from './lib/paths'

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

/** Static navigation menu used by the admin shell, mixing standalone nav items and grouped sections with i18n-keyed titles. Every url goes through `adminPath`/`accountPath`. */
export const navItems: (NavItem | NavItemGroup)[] = [
  {
    title: 'navigation.dashboard',
    url: adminPath('/'),
    icon: Home,
    children: [
      {
        title: 'navigation.dashboardSales',
        url: adminPath('/'),
      },
    ],
  },
  {
    title: 'navigation.administration',
    items: [
      {
        title: 'navigation.users',
        url: adminPath('/users'),
        icon: Users,
        children: [
          {
            title: 'navigation.usersList',
            url: adminPath('/users'),

          },
          {
            title: 'navigation.usersCreate',
            url: adminPath('/users/create'),

          },
          {
            title: 'navigation.usersUpdate',
            url: adminPath('/users/{id}/update'),

            show: false,
          },
        ],
      },
      {
        title: 'navigation.roles',
        url: adminPath('/roles'),
        icon: Shield,
        children: [
          {
            title: 'navigation.rolesList',
            url: adminPath('/roles'),

          },
          {
            title: 'navigation.rolesCreate',
            url: adminPath('/roles/create'),

          },
          {
            title: 'navigation.rolesUpdate',
            url: adminPath('/roles/{id}/update'),

            show: false,
          },
          {
            title: 'navigation.rolesChangePermissions',
            url: adminPath('/roles/{id}/change-permissions'),

            show: false,
          },
        ],
      },
      {
        title: 'navigation.seo',
        url: adminPath('/seo'),
        icon: Search,
        children: [
          {
            title: 'navigation.seoList',
            url: adminPath('/seo'),
          },
          {
            title: 'navigation.seoCreate',
            url: adminPath('/seo/create'),
          },
          {
            title: 'navigation.seoUpdate',
            url: adminPath('/seo/{id}/update'),
            show: false,
          },
        ],
      },
      {
        title: 'navigation.localization',
        url: adminPath('/localization'),
        icon: Languages,
      },
      {
        title: 'navigation.settings',
        url: adminPath('/settings'),
        icon: Settings,
      },
      {
        title: 'navigation.notifications',
        url: adminPath('/notifications'),
        icon: Bell,
        children: [
          {
            title: 'navigation.notificationsDetail',
            url: adminPath('/notifications/{id}'),
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
        url: adminPath('/ui/form-elements'),
        icon: FormInput,
      },
      {
        title: 'navigation.uiButtons',
        url: adminPath('/ui/buttons'),
        icon: Zap,
      },
      {
        title: 'navigation.uiCards',
        url: adminPath('/ui/cards'),
        icon: Palette,
      },
      {
        title: 'navigation.uiDatePicker',
        url: adminPath('/ui/date-picker'),
        icon: Calendar,
      },
      {
        title: 'navigation.uiDateView',
        url: adminPath('/ui/date-view'),
        icon: Clock,
      },
      {
        title: 'navigation.uiTreeview',
        url: adminPath('/ui/treeview'),
        icon: TreePine,
      },
      {
        title: 'navigation.uiFileUpload',
        url: adminPath('/ui/file-upload'),
        icon: Upload,
      },
      {
        title: 'navigation.uiTooltips',
        url: adminPath('/ui/tooltips'),
        icon: Palette,
      },
    ],
  },
  // efn:examples:end
  {
    title: 'navigation.changePassword',
    url: accountPath('/change-password'),
    icon: Lock,
    show: false,
  },
  {
    title: 'navigation.profile',
    url: accountPath('/profile'),
    icon: User,
    show: false,
  },
]
