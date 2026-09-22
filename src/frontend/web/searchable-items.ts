/** Lightweight title/url pair used by the global search palette to surface in-app destinations. */
export interface SearchableItem {
  title: string
  url: string
}

/** Flat list of destinations offered by the global search feature, with i18n-keyed titles and their target urls. */
export const searchableItems: SearchableItem[] = [
  {
    title: 'search.dashboard',
    url: '/admin',
  },
  {
    title: 'search.users',
    url: '/admin/users/list',

  },
  {
    title: 'search.usersCreate',
    url: '/admin/users/create',

  },
  {
    title: 'search.editions',
    url: '/admin/editions/list',

  },
  {
    title: 'search.editionsCreate',
    url: '/admin/editions/create',

  },
  {
    title: 'search.roles',
    url: '/admin/roles/list',

  },
  {
    title: 'search.rolesCreate',
    url: '/admin/roles/create',

  },
  {
    title: 'search.tenants',
    url: '/admin/tenants/list',
  },
  {
    title: 'search.tenantsCreate',
    url: '/admin/tenants/create',
  },
  {
    title: 'search.profile',
    url: '/profile',
  },
  {
    title: 'search.changePassword',
    url: '/change-password',
  },
  {
    title: 'search.notifications',
    url: '/admin/notifications/list',
  },

  {
    title: 'search.uiFormElements',
    url: '/admin/ui/form-elements',
  },
  {
    title: 'search.uiButtons',
    url: '/admin/ui/buttons',
  },
  {
    title: 'search.uiCards',
    url: '/admin/ui/cards',
  },
  {
    title: 'search.uiDatePicker',
    url: '/admin/ui/date-picker',
  },
  {
    title: 'search.uiDateView',
    url: '/admin/ui/date-view',
  },
  {
    title: 'search.uiTreeview',
    url: '/admin/ui/treeview',
  },
  {
    title: 'search.uiFileUpload',
    url: '/admin/ui/file-upload',
  },
  {
    title: 'search.uiTooltips',
    url: '/admin/ui/tooltips',
  },
]
