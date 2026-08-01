'use client'
import React from 'react'
import { TreeNode, TreeView, CodeShowcase } from '@/components/ui'

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
  className="panel w-75"
  data={treeData}
  onNodeClick={(node) => console.log("Clicked:", node)}
/>`

  const selectableTreeCode = `<TreeView
  className="panel w-75"
  data={treeData}
  enableSelection={true}
  defaultSelectedIds={["1.1.1, 1.1.2"]}
  onSelectionChange={(selectedIds) => {
    console.log("Selected IDs:", selectedIds)
  }}
/>`

  const expandedTreeCode = `<TreeView
  className="panel w-75"
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
  className="panel w-87.5"
  data={orgData}
  defaultExpandedIds={["ceo", "cto", "cfo"]}
  enableSelection={true}
/>`

  return (
    <div className="container mx-auto space-y-8 p-6">
      <div className="mb-8">
        <h1 className="mb-2 text-3xl font-bold">TreeView Component</h1>
        <p className="text-white-dark">Hierarchical tree structure component with expand/collapse, selection, and custom data support.</p>
      </div>

      <div className="grid gap-8">
        {/* Basic TreeView */}
        <CodeShowcase
          title="Basic TreeView"
          description="Simple tree structure with clickable nodes"
          code={basicTreeCode}
          preview={
            <div className="flex justify-center">
              <TreeView className="panel w-75" data={basicTreeData} onNodeClick={handleNodeClick} />
            </div>
          }
        />

        {/* Selectable TreeView */}
        <CodeShowcase
          title="Selectable TreeView"
          description="TreeView with selection support and default selected items"
          code={selectableTreeCode}
          preview={
            <div className="flex justify-center">
              <TreeView className="panel w-75" data={basicTreeData} enableSelection={true} defaultSelectedIds={selectableDefaultSelectedIds} onSelectionChange={handleSelectionChange} />
            </div>
          }
        />

        {/* Pre-expanded TreeView */}
        <CodeShowcase
          title="Pre-expanded TreeView"
          description="TreeView with specific nodes expanded by default"
          code={expandedTreeCode}
          preview={
            <div className="flex justify-center">
              <TreeView className="panel w-75" data={advancedTreeData} defaultExpandedIds={advancedDefaultExpandedIds} onNodeClick={handleNodeClick} />
            </div>
          }
        />

        {/* Organization Chart TreeView */}
        <CodeShowcase
          title="Organization Chart"
          description="Real-world example showing organizational hierarchy"
          code={organizationTreeCode}
          preview={
            <div className="flex justify-center">
              <TreeView
                className="panel w-87.5"
                data={organizationData}
                defaultExpandedIds={organizationDefaultExpandedIds}
                enableSelection={true}
                onNodeClick={handleEmployeeNodeClick}
                onSelectionChange={handleEmployeeSelectionChange}
              />
            </div>
          }
        />

        {/* Feature Showcase */}
        <CodeShowcase
          title="All Features Combined"
          description="TreeView with all features enabled: selection, pre-expansion, click handlers"
          code={`<TreeView
  className="panel w-100"
  data={complexTreeData}
  enableSelection={true}
  defaultExpandedIds={["1", "1.1"]}
  defaultSelectedIds={["1.1.1"]}
  onNodeClick={(node) => console.log("Clicked:", node)}
  onSelectionChange={(ids) => console.log("Selection:", ids)}
/>`}
          preview={
            <div className="flex justify-center">
              <TreeView
                className="panel w-100"
                data={advancedTreeData}
                enableSelection={true}
                defaultExpandedIds={featuresDefaultExpandedIds}
                defaultSelectedIds={featuresDefaultSelectedIds}
                onNodeClick={handleNodeClick}
                onSelectionChange={handleSelectionChange}
              />
            </div>
          }
        />
      </div>
    </div>
  )
}
