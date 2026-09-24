#!/usr/bin/env node
/**
 * The live smoke check: does the API this checkout builds actually serve?
 *
 *   node scripts/smoke.mjs http://localhost:5000
 *
 * verify.mjs runs it after an API change, against a service it has proven is running this
 * checkout. It asks three questions a green build cannot answer: the host starts and reports
 * healthy, the endpoint surface still generates its OpenAPI document (a broken endpoint
 * registration or a Mapperly/validator mismatch fails here, at startup, not at compile
 * time), and an authenticated endpoint still refuses an anonymous caller.
 */
const api = (process.argv[2] || "http://localhost:5000").replace(/\/$/, "");

const checks = [
  { name: "health", path: "/health", expect: (res) => res.status === 200 },
  { name: "OpenAPI document", path: "/swagger/v1/swagger.json", expect: (res) => res.status === 200 },
  { name: "anonymous caller is refused", path: "/api/account/get-info", expect: (res) => res.status === 401 },
];

let failed = 0;
for (const check of checks) {
  let outcome;
  try {
    const res = await fetch(`${api}${check.path}`, { signal: AbortSignal.timeout(15000), redirect: "manual" });
    outcome = check.expect(res) ? "PASS" : `FAIL (HTTP ${res.status})`;
  } catch (err) {
    outcome = `FAIL (${err.cause?.code ?? err.name})`;
  }
  if (outcome !== "PASS") failed += 1;
  console.log(`  ${outcome.padEnd(16)} ${check.name}  ${check.path}`);
}

if (failed > 0) {
  console.error(`\n${failed} smoke check(s) failed against ${api}.`);
  process.exit(1);
}
console.log(`\nThe API at ${api} passed every smoke check.`);
