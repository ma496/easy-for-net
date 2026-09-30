import { strict as assert } from "node:assert";
import { test } from "node:test";
import { ensureDependencies, taskDependencies } from "../lib/dependencies.mjs";

test("the cycle's preflight and the service's dependencies merge by name, case aside", () => {
  const deps = taskDependencies({
    cycle: { preflight: [{ name: "Postgres", healthyWhen: "pg-probe Testing", whenMissing: "start it" }] },
    verify: {
      service: {
        dependsOn: [
          { name: "postgres", healthyWhen: "pg-probe", start: "pg-start", waitSeconds: 10 },
          { name: "redis", healthyWhen: "redis-probe" },
        ],
      },
    },
  });
  assert.deepEqual(
    deps.map((d) => [d.name, d.healthyWhen, d.start ?? null]),
    [
      ["Postgres", "pg-probe Testing", "pg-start"],
      ["redis", "redis-probe", null],
    ],
  );
});

test("an entry without a probe is not a dependency the runner can check", () => {
  assert.deepEqual(taskDependencies({ cycle: { preflight: [{ name: "x" }] } }), []);
  assert.deepEqual(taskDependencies(undefined), []);
});

test("a healthy dependency passes, and one that is down with no start command is reported", () => {
  const up = { name: "up", healthyWhen: `node -e "process.exit(0)"` };
  const down = { name: "down", healthyWhen: `node -e "process.exit(1)"`, whenMissing: "start down" };
  assert.deepEqual(ensureDependencies([up, down]).map((d) => d.name), ["down"]);
});
