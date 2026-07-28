'use client'
import { TreeNode, TreeView } from '@/components/ui/treeview'
import { PermissionGroupDefinition, PermissionDefinition } from '@/store/api/identity/permissions/permissions-dtos'
import { PermissionDto } from '@/store/api/identity/permissions/permissions-dtos'
import { useGetDefinePermissionsQuery } from '@/store/api/identity/permissions/permissions-api'
import { useRoleGetQuery } from '@/store/api/identity/roles/roles-api'
import { useGetPermissionsQuery } from '@/store/api/identity/permissions/permissions-api'
import { useChangePermissionsMutation } from '@/store/api/identity/roles/roles-api'
import { useEffect, useState, useCallback, useMemo } from 'react'
import { Button } from '@/components/ui/button'
import { ApiErrorMessages } from '@/components/ui/api-error-messages'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import AppLoading from '@/components/layouts/app-loading'
import { Search } from 'lucide-react'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Converts the API permission definitions plus the assigned permissions into a hierarchical list of TreeNode objects for the TreeView component.
 */
const toTreeNodes = (groups: PermissionGroupDefinition[], permissions: PermissionDto[]): TreeNode[] => {
  let count = 1
  const buildNode = (definePermission: PermissionDefinition): TreeNode => {
    const permission = permissions.find((p) => p.name === definePermission.name)
    return {
      id: permission ? permission.id : `${definePermission.name}-${count++}`,
      label: definePermission.displayName,
      children: definePermission.children?.map(buildNode) || [],
      show: true,
    }
  }

  return groups.map((g) => ({
    id: `group-${g.groupName}`,
    label: g.groupName,
    children: g.permissions.map(buildNode),
    show: true,
  }))
}

/**
 * Recursively checks whether a tree node with the given id has any children, used to filter out group nodes from the selected permissions payload.
 */
const isHaveChild = (id: string, nodes: TreeNode[]): boolean => {
  if (!nodes || nodes.length === 0) return false

  const findNode = nodes.find((n) => n.id == id)

  if (findNode?.children && findNode.children.length > 0) {
    return true
  }

  if (!findNode) {
    for (const node of nodes) {
      if (node.children && node.children.length > 0) {
        const result = isHaveChild(id, node.children)
        if (result) {
          return true
        }
      }
    }
  }

  return false
}

/**
 * Props for the ChangePermissionsForm, supplying the id of the role whose permissions are being edited.
 */
interface ChangePermissionsFormProps {
  roleId: string
}

/**
 * Interactive client-side form for editing a role's permission set, organized as selectable permission groups in a TreeView with search and submit handling.
 */
export const ChangePermissionsForm = ({ roleId }: ChangePermissionsFormProps) => {
  const { data: role, isFetching: isFetchingRole } = useRoleGetQuery({ id: roleId }, { refetchOnMountOrArgChange: true })
  const { data: definePermissionsRes, isLoading: isLoadingDefinePermissions, error: getDefinePermissionsError } = useGetDefinePermissionsQuery()
  const { data: permissionsRes, isLoading: isLoadingPermissions, error: getPermissionsError } = useGetPermissionsQuery()
  const [changePermissionsApi, { isLoading: isChangingPermissions }] = useChangePermissionsMutation()
  const [changedPermissions, setChangedPermissions] = useState<string[]>([])
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const [search, setSearch] = useState('')
  const [activeGroup, setActiveGroup] = useState<string | null>(null)

  const permissionTreeNodes = useMemo(() => definePermissionsRes && permissionsRes ? toTreeNodes(definePermissionsRes.groups, permissionsRes.permissions) : [], [definePermissionsRes, permissionsRes])

  const isLoading = isFetchingRole || isLoadingDefinePermissions || isLoadingPermissions

  useEffect(() => {
    if (permissionTreeNodes.length > 0 && !activeGroup) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setActiveGroup(permissionTreeNodes[0].id)
    }
  }, [permissionTreeNodes, activeGroup])

  const filterTreeNodes = useCallback((nodes: TreeNode[], query: string): void => {
    const filterRecursive = (currentNodes: TreeNode[], currentQuery: string) => {
      if (!currentQuery) {
        currentNodes.forEach((n) => {
          n.show = true
          if (n.children && n.children.length > 0) {
            filterRecursive(n.children, currentQuery)
          }
        })
      } else {
        const normalizedQuery = currentQuery.trim().toLowerCase()
        currentNodes.forEach((n) => {
          if (n.children && n.children.length > 0) {
            filterRecursive(n.children, currentQuery)
            const matchesQuery = n.label.toLowerCase().includes(normalizedQuery)
            const hasVisibleChild = n.children.some((child) => child.show === true)
            n.show = matchesQuery || hasVisibleChild
          } else {
            n.show = n.label.toLowerCase().includes(normalizedQuery)
          }
        })
      }
    }
    filterRecursive(nodes, query)
  }, [])

  useEffect(() => {
    if (role) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setChangedPermissions(role.permissions ?? [])
    }
  }, [role])

  const filteredTreeNodes = useMemo(() => {
    const cloneNodes = (nodes: TreeNode[]): TreeNode[] => {
      return nodes.map(n => ({
        ...n,
        children: n.children ? cloneNodes(n.children) : undefined
      }))
    }
    const cloned = cloneNodes(permissionTreeNodes)

    filterTreeNodes(cloned, '') // Reset all cleanly first

    if (search && activeGroup) {
      const activeNode = cloned.find(n => n.id === activeGroup)
      if (activeNode && activeNode.children) {
        filterTreeNodes(activeNode.children, search)
      }
    }
    return cloned
  }, [permissionTreeNodes, search, activeGroup, filterTreeNodes])

  const activeGroupNode = useMemo(() => filteredTreeNodes.find(n => n.id === activeGroup), [filteredTreeNodes, activeGroup])

  const handleSubmit = async () => {
    const response = await changePermissionsApi({
      id: roleId,
      permissions: changedPermissions,
    })
    if (response.error) {
      apiErrorAlert(response.error)
      return
    }

    successToast.fire({
      text: t('page.roles.permissionsUpdateSuccess'),
    })
    router.push('/admin/roles/list')
  }

  const handleSelectionChange = useCallback((selectedIds: string[]) => {
    const permissions = selectedIds.filter((id) => !isHaveChild(id, permissionTreeNodes))
    setChangedPermissions(permissions)
  }, [permissionTreeNodes])

  if (isLoading) {
    return (
      <div className="flex h-full items-center justify-center min-h-100">
        <AppLoading />
      </div>
    )
  }

  if (!isLoading && (getDefinePermissionsError || getPermissionsError)) {
    return (
      <div className="flex h-full items-center justify-center min-h-100">
        <ApiErrorMessages error={getDefinePermissionsError || getPermissionsError} />
      </div>
    )
  }

  return (
    <div className="h-full flex justify-center">
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <span className="text-lg font-semibold">
            {role?.name} - {t('page.roles.permissions')}
          </span>

          <div className="grid grid-cols-1 md:grid-cols-4 gap-4 min-h-100">
            <div className="md:col-span-1 border-r ltr:border-r rtl:border-l rtl:border-r-0 border-gray-200 dark:border-gray-700 flex flex-col pt-2 ltr:pr-4 rtl:pl-4">
              <div className="font-semibold mb-3 text-gray-800 dark:text-gray-100 uppercase tracking-wider text-xs">{t('page.roles.permissionGroups') || 'Groups'}</div>
              <div className="flex flex-col space-y-1">
                {permissionTreeNodes.map((group) => (
                  <button
                    key={group.id}
                    type="button"
                    className={`cursor-pointer px-3 py-2 rounded-md transition-colors ${activeGroup === group.id ? 'bg-primary text-white' : 'hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-300'}`}
                    onClick={() => {
                      setActiveGroup(group.id)
                      setSearch('')
                    }}
                  >
                    {group.label}
                  </button>
                ))}
              </div>
            </div>
            <div className="md:col-span-3 pt-2">
              <div className="flex justify-between items-center mb-3">
                <div className="font-semibold text-gray-800 dark:text-gray-100 uppercase tracking-wider text-xs">{activeGroupNode?.label || t('page.roles.permissions')} Permissions</div>
                <div className="relative max-w-xs w-full">
                  <input type="text" name="searchPermissions" className="form-input w-full ltr:pl-9 rtl:pr-9 py-1.5! text-sm" placeholder={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} />
                  <Search className="absolute top-1/2 h-4 w-4 -translate-y-1/2 text-gray-300 ltr:left-2 rtl:right-2 dark:text-gray-600" />
                </div>
              </div>
              {activeGroupNode && (
                <TreeView
                  data={activeGroupNode.children ?? []}
                  defaultSelectedIds={changedPermissions}
                  expandAll={true}
                  enableSelection={true}
                  onSelectionChange={handleSelectionChange}
                />
              )}
            </div>
          </div>

        </div>
        <div className="flex justify-end mt-4">
          <Button variant="default" onClick={handleSubmit} isLoading={isChangingPermissions}>
            {t('common.save')}
          </Button>
        </div>
      </div>
    </div>
  )
}
