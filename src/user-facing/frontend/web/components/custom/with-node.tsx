import React from 'react'

/** The stand-in a translated sentence is given for a value {@link withNode} puts back as an element: U+2063, an invisible separator no translation contains. */
export const NODE_MARK = String.fromCharCode(0x2063)

/**
 * Puts `node` where a translated sentence carries {@link NODE_MARK} - pass the mark as the variable's
 * value, `t('key', { name: NODE_MARK })` - so a name or an address can sit inside the sentence as its
 * own element (a `<bdi>` that keeps its own direction). A sentence that lost its placeholder in
 * translation still shows the value, after it.
 */
export const withNode = (text: string, node: React.ReactNode): React.ReactNode => {
  const at = text.indexOf(NODE_MARK)
  if (at < 0) {
    return (
      <>
        {text} {node}
      </>
    )
  }
  return (
    <>
      {text.slice(0, at)}
      {node}
      {text.slice(at + NODE_MARK.length)}
    </>
  )
}
