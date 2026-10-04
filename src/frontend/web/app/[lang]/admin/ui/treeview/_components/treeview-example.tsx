'use client'
import React from 'react'
import { TreeNode, TreeView, CodeShowcase } from '@/components/ui'
import { ShowcasePreview } from '../../_components/showcase-preview'

const basicTreeData: TreeNode[] = [
  {
    id: '1',
    label: 'Documents',
    children: [
      {
        id: '1.1',
        label: 'Projects',
        children: [
          { id: '1.1.1', label: 'Project A' },
          { id: '1.1.2', label: 'Project B' },
        ],
      },
      {
        id: '1.2',
        label: 'Reports',
        children: [
          { id: '1.2.1', label: 'Q1 Report' },
          { id: '1.2.2', label: 'Q2 Report' },
        ],
      },
    ],
  },
  {
    id: '2',
    label: 'Images',
    children: [
      { id: '2.1', label: 'Screenshots' },
      { id: '2.2', label: 'Photos' },
    ],
  },
]

const advancedTreeData: TreeNode[] = [
  {
    id: '1',
    label: 'Root Item 1',
    children: [
      {
        id: '1.1',
        label: 'Child Item 1.1',
        children: [
          { id: '1.1.1', label: 'Grandchild 1.1.1' },
          { id: '1.1.2', label: 'Grandchild 1.1.2' },
          { id: '1.1.3', label: 'Grandchild 1.1.3' },
        ],
      },
      {
        id: '1.2',
        label: 'Child Item 1.2',
        children: [
          { id: '1.2.1', label: 'Grandchild 1.2.1' },
          { id: '1.2.2', label: 'Grandchild 1.2.2' },
          { id: '1.2.3', label: 'Grandchild 1.2.3' },
        ],
      },
    ],
  },
  {
    id: '2',
    label: 'Root Item 2',
    data: { customData: 'example' },
  },
  {
    id: '3',
    label: 'Root Item 3',
    children: [
      {
        id: '3.1',
        label: 'Child Item 3.1',
      },
    ],
  },
]

const organizationData: TreeNode[] = [
  {
    id: 'ceo',
    label: 'CEO',
    children: [
      {
        id: 'cto',
        label: 'CTO',
        children: [
          { id: 'dev-lead', label: 'Development Lead' },
          { id: 'qa-lead', label: 'QA Lead' },
        ],
      },
      {
        id: 'cfo',
        label: 'CFO',
        children: [
          { id: 'accountant', label: 'Senior Accountant' },
          { id: 'analyst', label: 'Financial Analyst' },
        ],
      },
      {
        id: 'hr',
        label: 'HR Director',
        children: [
          { id: 'recruiter', label: 'Recruiter' },
          { id: 'coordinator', label: 'HR Coordinator' },
        ],
      },
    ],
  },
]

/**
 * Interactive client-side showcase component that demonstrates the TreeView in basic, selectable, pre-expanded, organization-chart, and fully-featured configurations.
 */
export const TreeviewExample = () => {
  // Stabilize array references with useMemo
  const selectableDefaultSelectedIds = React.useMemo(() => ['1.1.1', '1.1.2'], [])
  const advancedDefaultExpandedIds = React.useMemo(() => ['1', '1.1'], [])
  const organizationDefaultExpandedIds = React.useMemo(() => ['ceo', 'cto', 'cfo'], [])
  const featuresDefaultExpandedIds = React.useMemo(() => ['1', '1.1'], [])
  const featuresDefaultSelectedIds = React.useMemo(() => ['1.1.1'], [])

  // Stabilize callback functions with useCallback
  const handleNodeClick = React.useCallback((node: TreeNode) => {
    console.log('Clicked node:', node)
  }, [])

  const handleSelectionChange = React.useCallback((selectedIds: string[]) => {
    console.log('Selected IDs:', selectedIds)
  }, [])

  const handleEmployeeNodeClick = React.useCallback((node: TreeNode) => {
    console.log('Selected employee:', node)
  }, [])

  const handleEmployeeSelectionChange = React.useCallback((selectedIds: string[]) => {
    console.log('Selected employees:', selectedIds)
  }, [])

  const basicTreeCode = `const treeData: TreeNode[] = [
  {
    id: "1",
    label: "Documents",
    children: [
      {
        id: "1.1",
        label: "Projects",
        children: [
          { id: "1.1.1", label: "Project A" },
          { id: "1.1.2", label: "Project B" },
        ]
      }
    ]
  }
]

<TreeView
  className="panel w-full max-w-sm"
  data={treeData}
  onNodeClick={(node) => console.log("Clicked:", node)}
/>`

  const selectableTreeCode = `<TreeView
  className="panel w-full max-w-sm"
  data={treeData}
  enableSelection={true}
  defaultSelectedIds={["1.1.1", "1.1.2"]}
  onSelectionChange={(selectedIds) => {
    console.log("Selected IDs:", selectedIds)
  }}
/>`

  const expandedTreeCode = `<TreeView
  className="panel w-full max-w-sm"
  data={treeData}
  defaultExpandedIds={["1", "1.1"]}
  expandAll={false}
/>`

  const organizationTreeCode = `const orgData: TreeNode[] = [
  {
    id: "ceo",
    label: "CEO",
    children: [
      {
        id: "cto",
        label: "CTO",
        children: [
          { id: "dev-lead", label: "Development Lead" },
          { id: "qa-lead", label: "QA Lead" },
        ]
      }
    ]
  }
]

<TreeView
  className="panel w-full max-w-sm"
  data={orgData}
  defaultExpandedIds={["ceo", "cto", "cfo"]}
  enableSelection={true}
/>`

  return (
    <div className="space-y-6">
      <div className="grid items-start gap-6 xl:grid-cols-2">
        {/* Basic TreeView */}
        <CodeShowcase
          title="Basic tree"
          description="Simple tree structure with clickable nodes"
          code={basicTreeCode}
          preview={
            <ShowcasePreview>
              <TreeView className="panel w-full max-w-sm" data={basicTreeData} onNodeClick={handleNodeClick} />
            </ShowcasePreview>
          }
        />

        {/* Selectable TreeView */}
        <CodeShowcase
          title="Selection"
          description="TreeView with selection support and default selected items"
          code={selectableTreeCode}
          preview={
            <ShowcasePreview>
              <TreeView className="panel w-full max-w-sm" data={basicTreeData} enableSelection={true} defaultSelectedIds={selectableDefaultSelectedIds} onSelectionChange={handleSelectionChange} />
            </ShowcasePreview>
          }
        />

        {/* Pre-expanded TreeView */}
        <CodeShowcase
          title="Pre-expanded"
          description="TreeView with specific nodes expanded by default"
          code={expandedTreeCode}
          preview={
            <ShowcasePreview>
              <TreeView className="panel w-full max-w-sm" data={advancedTreeData} defaultExpandedIds={advancedDefaultExpandedIds} onNodeClick={handleNodeClick} />
            </ShowcasePreview>
          }
        />

        {/* Organization Chart TreeView */}
        <CodeShowcase
          title="Organization chart"
          description="Real-world example showing organizational hierarchy"
          code={organizationTreeCode}
          preview={
            <ShowcasePreview>
              <TreeView
                className="panel w-full max-w-sm"
                data={organizationData}
                defaultExpandedIds={organizationDefaultExpandedIds}
                enableSelection={true}
                onNodeClick={handleEmployeeNodeClick}
                onSelectionChange={handleEmployeeSelectionChange}
              />
            </ShowcasePreview>
          }
        />

        {/* Feature Showcase */}
        <CodeShowcase
          className="xl:col-span-2"
          title="All features combined"
          description="TreeView with all features enabled: selection, pre-expansion, click handlers"
          code={`<TreeView
  className="panel w-full max-w-sm"
  data={complexTreeData}
  enableSelection={true}
  defaultExpandedIds={["1", "1.1"]}
  defaultSelectedIds={["1.1.1"]}
  onNodeClick={(node) => console.log("Clicked:", node)}
  onSelectionChange={(ids) => console.log("Selection:", ids)}
/>`}
          preview={
            <ShowcasePreview>
              <TreeView
                className="panel w-full max-w-sm"
                data={advancedTreeData}
                enableSelection={true}
                defaultExpandedIds={featuresDefaultExpandedIds}
                defaultSelectedIds={featuresDefaultSelectedIds}
                onNodeClick={handleNodeClick}
                onSelectionChange={handleSelectionChange}
              />
            </ShowcasePreview>
          }
        />
      </div>
    </div>
  )
}
