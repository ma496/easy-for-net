/** One documented prop of a component. */
export interface PropRow {
  name: string
  type: string
  defaultValue: string
  description: string
}

/** Props for PropsTable, the reference list of a component's props. */
interface PropsTableProps {
  rows: PropRow[]
}

/**
 * PropsTable documents a component's props: a table from md up, and one stacked block per prop on phones so nothing scrolls sideways.
 */
export const PropsTable = ({ rows }: PropsTableProps) => (
  <>
    <div className="hidden overflow-hidden rounded-lg border border-border md:block">
      <table className="table-base">
        <thead>
          <tr>
            <th>Prop</th>
            <th>Type</th>
            <th>Default</th>
            <th>Description</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.name}>
              <td className="font-mono text-[13px] font-medium whitespace-nowrap text-primary">{row.name}</td>
              <td className="font-mono text-[13px] text-muted-foreground">{row.type}</td>
              <td className="font-mono text-[13px] text-muted-foreground">{row.defaultValue}</td>
              <td className="text-muted-foreground">{row.description}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
    <dl className="divide-y divide-border rounded-lg border border-border md:hidden">
      {rows.map((row) => (
        <div key={row.name} className="space-y-1 p-4">
          <dt className="font-mono text-[13px] font-medium break-all text-primary">{row.name}</dt>
          <dd className="font-mono text-xs break-words text-muted-foreground">
            {row.type} · {row.defaultValue}
          </dd>
          <dd className="text-sm text-muted-foreground">{row.description}</dd>
        </div>
      ))}
    </dl>
  </>
)
