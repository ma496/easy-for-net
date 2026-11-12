import { accountPath, adminPath } from './lib/paths'

/** Lightweight title/url pair used by the global search palette to surface in-app destinations. */
export interface SearchableItem {
  title: string
  url: string
}

/** Flat list of destinations offered by the global search feature, with i18n-keyed titles and their target urls. */
export const searchableItems: SearchableItem[] = [
  {
    title: 'search.dashboard',
    url: adminPath('/'),
  },
  {
    title: 'search.users',
    url: adminPath('/users'),
  },
  {
    title: 'search.usersCreate',
    url: adminPath('/users/create'),

  },
  {
    title: 'search.roles',
    url: adminPath('/roles'),

  },
  {
    title: 'search.rolesCreate',
    url: adminPath('/roles/create'),

  },
  {
    title: 'search.seo',
    url: adminPath('/seo'),
  },
  {
    title: 'search.seoCreate',
    url: adminPath('/seo/create'),
  },
  {
    title: 'search.profile',
    url: accountPath('/profile'),
  },
  {
    title: 'search.changePassword',
    url: accountPath('/change-password'),
  },
  {
    title: 'search.localization',
    url: adminPath('/localization'),
  },
  {
    title: 'search.settings',
    url: adminPath('/settings'),
  },
  {
    title: 'search.notifications',
    url: adminPath('/notifications'),
  },
  // efn:examples:start
  {
    title: 'search.uiFormElements',
    url: adminPath('/ui/form-elements'),
  },
  {
    title: 'search.uiButtons',
    url: adminPath('/ui/buttons'),
  },
  {
    title: 'search.uiCards',
    url: adminPath('/ui/cards'),
  },
  {
    title: 'search.uiDatePicker',
    url: adminPath('/ui/date-picker'),
  },
  {
    title: 'search.uiDateView',
    url: adminPath('/ui/date-view'),
  },
  {
    title: 'search.uiTreeview',
    url: adminPath('/ui/treeview'),
  },
  {
    title: 'search.uiFileUpload',
    url: adminPath('/ui/file-upload'),
  },
  {
    title: 'search.uiTooltips',
    url: adminPath('/ui/tooltips'),
  },
  // efn:examples:end
]
