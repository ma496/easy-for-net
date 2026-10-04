'use client'
import { TreeNode, Button, Badge, ApiErrorMessages } from '@/components/ui'
import { PermissionGroupDefinition, PermissionDefinition, PermissionDto, useGetDefinePermissionsQuery, useGetPermissionsQuery, useRoleGetQuery, useChangePermissionsMutation } from '@/store/api/identity'
import { useEffect, useState, useCallback, useMemo, useId } from 'react'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { AppLoading } from '@/components/layouts'
import { Search, SearchX, ShieldOff } from 'lucide-react'
import { apiErrorAlert, cn, successToast } from '@/lib/utils'

/**
 * Converts the API permission definitions plus the assigned permissions into a hierarchical list of TreeNode objects for the permission tree.
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

/** Whether a node has children of its own, i.e. is a branch of the tree rather than a grantable leaf. */
const hasChildren = (node: TreeNode) => !!node.children && node.children.length > 0

/**
 * The ids of every leaf under a node (the node itself when it is a leaf). A branch is checked exactly
 * when all of its leaves are, so selecting or clearing a branch selects or clears these.
 */
const leafIds = (node: TreeNode): string[] => (hasChildren(node) ? node.children!.flatMap(leafIds) : [node.id])

/**
 * The leaves under a node that the search leaves on screen, which is what a branch or group checkbox
 * acts on and counts, so a select-all never grants or revokes a permission the search is hiding. A
 * branch matched by its own name but by none of its children stands for all of them, since its row
 * names them. Without a search every node is shown and this equals {@link leafIds}.
 */
const visibleLeafIds = (node: TreeNode): string[] => {
  if (!hasChildren(node)) return [node.id]
  const visible = node.children!.filter((child) => child.show !== false)
  return visible.length > 0 ? visible.flatMap(visibleLeafIds) : leafIds(node)
}

/** The tri-state of a checkbox standing for a set of leaves. */
type CheckState = 'checked' | 'mixed' | 'unchecked'

/** How many of the given leaves are selected, and the tri-state that count reads as. */
const countSelected = (ids: string[], selected: Set<string>): { count: number; state: CheckState } => {
  const count = ids.filter((id) => selected.has(id)).length
  return { count, state: count === 0 ? 'unchecked' : count === ids.length ? 'checked' : 'mixed' }
}

/** Props for PermissionCheck, one labelled checkbox row of the permission tree. */
interface PermissionCheckProps {
  label: string
  state: CheckState
  onChange: (checked: boolean) => void
  /** Branch rows read as headings: stronger label and the selected/total count at the end. */
  count?: { selected: number; total: number }
  className?: string
}

/**
 * PermissionCheck is a checkbox row whose whole width toggles it. A partly selected branch shows the
 * indeterminate mark, and checking it selects every leaf beneath.
 */
const PermissionCheck = ({ label, state, onChange, count, className }: PermissionCheckProps) => {
  const id = useId()
  return (
    <label htmlFor={id} className={cn('flex min-w-0 cursor-pointer items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors hover:bg-surface-2', className)}>
      <input
        id={id}
        type="checkbox"
        className="form-checkbox"
        checked={state === 'checked'}
        ref={(input) => {
          if (input) input.indeterminate = state === 'mixed'
        }}
        onChange={(e) => onChange(e.target.checked)}
      />
      <span className={cn('min-w-0 flex-1 wrap-break-word', count ? 'font-medium text-foreground' : 'text-foreground')}>{label}</span>
      {count && (
        <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
          {count.selected}/{count.total}
        </span>
      )}
    </label>
  )
}

/** Props for PermissionBranch, a permission with children rendered as a heading row over its children. */
interface PermissionBranchProps {
  node: TreeNode
  selected: Set<string>
  onToggle: (ids: string[], checked: boolean) => void
  /** Nested branches drop the card frame and indent instead. */
  nested?: boolean
}

/**
 * PermissionBranch renders a permission node: a leaf as a single checkbox row, a branch as a heading
 * row (checking it checks everything beneath) over a two-column grid of its visible children.
 */
const PermissionBranch = ({ node, selected, onToggle, nested = false }: PermissionBranchProps) => {
  const ids = visibleLeafIds(node)
  const { count, state } = countSelected(ids, selected)

  if (!hasChildren(node)) {
    return <PermissionCheck label={node.label} state={state} onChange={(checked) => onToggle(ids, checked)} />
  }

  const visibleChildren = node.children!.filter((child) => child.show !== false)

  return (
    <div className={cn(nested ? 'sm:col-span-2' : 'rounded-lg border border-border')}>
      <PermissionCheck
        label={node.label}
        state={state}
        onChange={(checked) => onToggle(ids, checked)}
        count={{ selected: count, total: ids.length }}
        className={cn(!nested && 'rounded-b-none border-b border-border bg-surface-2/60 py-2.5')}
      />
      <div className={cn('grid gap-x-2 sm:grid-cols-2', nested ? 'ps-7' : 'p-1.5')}>
        {visibleChildren.map((child) => (
          <PermissionBranch key={child.id} node={child} selected={selected} onToggle={onToggle} nested />
        ))}
      </div>
    </div>
  )
}

/**
 * Props for the ChangePermissionsForm, supplying the id of the role whose permissions are being edited.
 */
interface ChangePermissionsFormProps {
  roleId: string
}

/**
 * Interactive client-side form for editing a role's permission set: permission groups on the side, the
 * active group's permissions as a checkbox tree with search and select-all, and a sticky save footer.
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
  const searchId = useId()

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

  const selected = useMemo(() => new Set(changedPermissions), [changedPermissions])

  // Checking a row grants every leaf beneath it; clearing it revokes them. Other groups' grants are untouched.
  const handleToggle = useCallback((ids: string[], checked: boolean) => {
    setChangedPermissions((previous) => (checked ? Array.from(new Set([...previous, ...ids])) : previous.filter((id) => !ids.includes(id))))
  }, [])

  const handleSubmit = async () => {
    const response = await changePermissionsApi({
      id: roleId,
      // Only leaves are sent: a permission that has children is implied by them, never granted on its own.
      permissions: changedPermissions.filter((id) => !isHaveChild(id, permissionTreeNodes)),
    })
    if (response.error) {
      apiErrorAlert(response.error)
      return
    }

    successToast.fire({
      text: t('page.roles.permissionsUpdateSuccess'),
    })
    router.push('/admin/roles')
  }

  if (isLoading) {
    return (
      <div className="flex min-h-100 items-center justify-center p-6">
        <AppLoading />
      </div>
    )
  }

  if (!isLoading && (getDefinePermissionsError || getPermissionsError)) {
    return (
      <div className="p-4 sm:p-6">
        <ApiErrorMessages error={getDefinePermissionsError || getPermissionsError} dismissible={false} />
      </div>
    )
  }

  const allLeafIds = permissionTreeNodes.flatMap(leafIds)
  const totalSelected = countSelected(allLeafIds, selected).count
  // Only what the search leaves on screen: with nothing matching, the group checkbox selects nothing.
  const activeGroupLeafIds = (activeGroupNode?.children ?? []).filter((node) => node.show !== false).flatMap(visibleLeafIds)
  const activeGroupSelection = countSelected(activeGroupLeafIds, selected)
  const visiblePermissions = activeGroupNode?.children?.filter((node) => node.show !== false) ?? []

  return (
    <div>
      {/* Header: which role, and how much of the catalogue it holds */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-4 sm:px-6">
        <div className="min-w-0">
          <div className="text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">{t('page.roles.permissions')}</div>
          <div className="mt-0.5 truncate text-base font-semibold text-foreground">{role?.name}</div>
        </div>
        <Badge variant="primary" className="tabular-nums">
          {totalSelected}/{allLeafIds.length}
        </Badge>
      </div>

      {permissionTreeNodes.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
          <div className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
            <ShieldOff className="size-5" />
          </div>
          <p className="text-sm text-muted-foreground">{t('common.noResults')}</p>
        </div>
      ) : (
        <div className="grid md:grid-cols-[14rem_minmax(0,1fr)]">
          {/* Permission groups: wrapping chips on a phone, a side list from md */}
          <nav aria-label={t('page.roles.permissionGroups')} className="border-b border-border p-4 md:border-e md:border-b-0 sm:px-6 md:px-3 md:py-5">
            <div className="mb-2 hidden px-3 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase md:block">{t('page.roles.permissionGroups')}</div>
            <div className="flex flex-wrap gap-2 md:flex-col md:flex-nowrap md:gap-0.5">
              {permissionTreeNodes.map((group) => {
                const ids = leafIds(group)
                const groupCount = countSelected(ids, selected).count
                const isActive = activeGroup === group.id
                return (
                  <button
                    key={group.id}
                    type="button"
                    aria-current={isActive ? 'true' : undefined}
                    className={cn(
                      'flex cursor-pointer items-center justify-between gap-3 rounded-md border px-3 py-1.5 text-start text-sm transition-colors md:w-full md:border-transparent md:py-2',
                      isActive ? 'border-primary/30 bg-primary/10 font-medium text-primary' : 'border-border text-muted-foreground hover:bg-surface-2 hover:text-foreground',
                    )}
                    onClick={() => {
                      setActiveGroup(group.id)
                      setSearch('')
                    }}
                  >
                    <span className="min-w-0 truncate">{group.label}</span>
                    <span className={cn('shrink-0 text-xs tabular-nums', isActive ? 'text-primary' : 'text-subtle-foreground')}>
                      {groupCount}/{ids.length}
                    </span>
                  </button>
                )
              })}
            </div>
          </nav>

          {/* The active group's permissions */}
          <div className="min-w-0 p-4 sm:p-6">
            <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex min-w-0 items-center gap-1">
                <PermissionCheck
                  label={activeGroupNode?.label ?? ''}
                  state={activeGroupSelection.state}
                  onChange={(checked) => handleToggle(activeGroupLeafIds, checked)}
                  count={{ selected: activeGroupSelection.count, total: activeGroupLeafIds.length }}
                  className="-ms-3 text-base"
                />
              </div>
              <div className="relative w-full sm:max-w-64">
                <label htmlFor={searchId} className="sr-only">{t('common.search')}</label>
                <Search className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-subtle-foreground" />
                <input
                  id={searchId}
                  type="text"
                  name="searchPermissions"
                  className="form-input h-9 ps-9"
                  placeholder={t('common.search')}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>

            {visiblePermissions.length === 0 ? (
              <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border px-6 py-12 text-center">
                <div className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
                  <SearchX className="size-5" />
                </div>
                <p className="text-sm text-muted-foreground">{t('common.noResults')}</p>
              </div>
            ) : (
              <div className="space-y-3">
                {visiblePermissions.map((node) =>
                  hasChildren(node) ? (
                    <PermissionBranch key={node.id} node={node} selected={selected} onToggle={handleToggle} />
                  ) : (
                    <div key={node.id} className="rounded-lg border border-border p-1.5">
                      <PermissionBranch node={node} selected={selected} onToggle={handleToggle} />
                    </div>
                  ),
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Sticky action footer: Save stays in reach however long the group is */}
      <div className="sticky bottom-0 z-10 flex justify-end gap-2 rounded-b-xl border-t border-border bg-surface px-4 py-3 *:flex-1 sm:px-6 sm:*:flex-none">
        <Button variant="outline" onClick={() => router.push('/admin/roles')} disabled={isChangingPermissions}>
          {t('common.cancel')}
        </Button>
        <Button variant="default" onClick={handleSubmit} isLoading={isChangingPermissions}>
          {t('common.save')}
        </Button>
      </div>
    </div>
  )
}
