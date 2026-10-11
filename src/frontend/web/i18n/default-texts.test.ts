import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import ts from 'typescript'
import { describe, expect, it } from 'vitest'

// A missing key translates to `''`, and every call site carries the English to show instead:
// `t('common.save') || 'Save'`, `(await getServerTranslation(lang, 'brand.name')) || 'Easy For Net'`. That is
// what still renders when the API cannot be reached. Each default is a copy of the backend's shipped
// English value, and a copy drifts the first time the original is edited, so every call with a literal
// key must have a default, and a plain-string default must equal `en.json`. (A template-literal default
// interpolates variables, so only its presence is checked.)
const webRoot = fileURLToPath(new URL('..', import.meta.url))
const englishFile = fileURLToPath(new URL('../../../backend/Source/Features/Localization/Core/Resources/en.json', import.meta.url))

const flatten = (node: Record<string, unknown>, prefix = '', out: Record<string, string> = {}): Record<string, string> => {
  for (const [name, value] of Object.entries(node)) {
    const key = prefix ? `${prefix}.${name}` : name
    if (value && typeof value === 'object') flatten(value as Record<string, unknown>, key, out)
    else out[key] = String(value)
  }
  return out
}
const english = flatten(JSON.parse(readFileSync(englishFile, 'utf8')))

const SKIPPED_DIRECTORIES = new Set(['node_modules', 'i18n', 'public', 'coverage'])
const sourceFiles = (directory: string): string[] =>
  readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(directory, entry.name)
    if (entry.isDirectory()) return SKIPPED_DIRECTORIES.has(entry.name) || entry.name.startsWith('.') ? [] : sourceFiles(full)
    return /\.tsx?$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) && !entry.name.endsWith('.d.ts') ? [full] : []
  })

const isOr = (node: ts.Node | undefined): node is ts.BinaryExpression =>
  !!node && ts.isBinaryExpression(node) && (node.operatorToken.kind === ts.SyntaxKind.BarBarToken || node.operatorToken.kind === ts.SyntaxKind.QuestionQuestionToken)

/** The expression a translator call falls back to: the right operand of the `||` it is the left of, or of a `.then((text) => text || …)`. */
const defaultOf = (call: ts.CallExpression): ts.Expression | undefined => {
  let node: ts.Node = call
  if (call.parent && ts.isPropertyAccessExpression(call.parent) && call.parent.name.text === 'then') {
    const then = call.parent.parent
    const callback = ts.isCallExpression(then) ? then.arguments[0] : undefined
    return callback && ts.isArrowFunction(callback) && isOr(callback.body) ? callback.body.right : undefined
  }
  if (ts.isAwaitExpression(node.parent)) node = node.parent
  while (ts.isParenthesizedExpression(node.parent)) node = node.parent
  const parent = node.parent
  if (isOr(parent) && parent.left === node) return parent.right
  // `a || t('k') || 'X'` parses as `(a || t('k')) || 'X'`
  if (isOr(parent) && parent.right === node && isOr(parent.parent) && parent.parent.left === parent) return parent.parent.right
  return undefined
}

interface TranslatorCall {
  where: string
  key: string
  fallback: ts.Expression | undefined
}

const translatorCalls: TranslatorCall[] = sourceFiles(webRoot).flatMap((file) => {
  const text = readFileSync(file, 'utf8')
  if (!/\bt\(|getServerTranslation\(/.test(text)) return []
  const source = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true, file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS)
  const calls: TranslatorCall[] = []
  const visit = (node: ts.Node) => {
    if (ts.isCallExpression(node) && ts.isIdentifier(node.expression) && (node.expression.text === 't' || node.expression.text === 'getServerTranslation')) {
      const keyNode = node.arguments[node.expression.text === 't' ? 0 : 1]
      if (keyNode && (ts.isStringLiteral(keyNode) || ts.isNoSubstitutionTemplateLiteral(keyNode))) {
        const { line } = source.getLineAndCharacterOfPosition(node.getStart(source))
        calls.push({ where: `${path.relative(webRoot, file).replace(/\\/g, '/')}:${line + 1}`, key: keyNode.text, fallback: defaultOf(node) })
      }
    }
    ts.forEachChild(node, visit)
  }
  visit(source)
  return calls
})

describe('inline English defaults', () => {
  it('finds the translator calls it checks', () => {
    expect(translatorCalls.length).toBeGreaterThan(100)
  })

  it('names only keys the backend ships', () => {
    expect(translatorCalls.filter((call) => !(call.key in english)).map((call) => `${call.where} ${call.key}`)).toEqual([])
  })

  it('gives every call with a literal key an English default', () => {
    expect(translatorCalls.filter((call) => !call.fallback).map((call) => `${call.where} ${call.key}`)).toEqual([])
  })

  it('keeps every plain-string default equal to the shipped English value', () => {
    const drifted = translatorCalls
      .filter((call) => call.fallback && ts.isStringLiteral(call.fallback) && call.key in english && call.fallback.text !== english[call.key])
      .map((call) => `${call.where} ${call.key}: ${JSON.stringify((call.fallback as ts.StringLiteral).text)} != ${JSON.stringify(english[call.key])}`)

    expect(drifted).toEqual([])
  })
})
