/** Lightweight title/url pair used by the global search palette to surface in-app destinations. */
export interface SearchableItem {
  title: string
  url: string
}

/** Flat list of destinations offered by the global search feature, with i18n-keyed titles and their target urls. */
export const searchableItems: SearchableItem[] = [
  {
    title: 'search.dashboard',
    url: '/',
  },
  {
    title: 'search.users',
    url: '/users',

  },
  {
    title: 'search.usersCreate',
    url: '/users/create',

  },
  {
    title: 'search.editions',
    url: '/editions',

  },
  {
    title: 'search.editionsCreate',
    url: '/editions/create',

  },
  {
    title: 'search.roles',
    url: '/roles',

  },
  {
    title: 'search.rolesCreate',
    url: '/roles/create',

  },
  {
    title: 'search.tenants',
    url: '/tenants',
  },
  {
    title: 'search.tenantsCreate',
    url: '/tenants/create',
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
    title: 'search.localization',
    url: '/localization',
  },
  {
    title: 'search.settings',
    url: '/settings',
  },
  {
    title: 'search.notifications',
    url: '/notifications',
  },
  // efn:examples:start
  {
    title: 'search.uiFormElements',
    url: '/ui/form-elements',
  },
  {
    title: 'search.uiButtons',
    url: '/ui/buttons',
  },
  {
    title: 'search.uiCards',
    url: '/ui/cards',
  },
  {
    title: 'search.uiDatePicker',
    url: '/ui/date-picker',
  },
  {
    title: 'search.uiDateView',
    url: '/ui/date-view',
  },
  {
    title: 'search.uiTreeview',
    url: '/ui/treeview',
  },
  {
    title: 'search.uiFileUpload',
    url: '/ui/file-upload',
  },
  {
    title: 'search.uiTooltips',
    url: '/ui/tooltips',
  },
  // efn:examples:end
]
