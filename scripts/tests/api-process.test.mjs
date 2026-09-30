import { strict as assert } from "node:assert";
import { test } from "node:test";
import { matchApiProcesses } from "../lib/api-process.mjs";

const pids = (r) => r.map((p) => p.pid);

test("the apphost running from this checkout's bin directory is matched, however the path is spelled", () => {
  const listing = [
    { pid: 10, exe: "D:\\Projects\\app\\src\\backend\\Source\\bin\\Debug\\net10.0\\Backend.exe", commandLine: "" },
    { pid: 11, exe: "C:\\Program Files\\dotnet\\dotnet.exe", commandLine: "dotnet run --project src/backend/Source" },
  ];
  const r = matchApiProcesses(listing, "d:/projects/app/src/backend/Source/bin", { windows: true, selfPid: 1 });
  assert.deepEqual(pids(r), [10]);
});

test("dotnet running the API's dll is matched", () => {
  const listing = [
    { pid: 20, exe: "", commandLine: 'dotnet "/repo/src/backend/Source/bin/Debug/net10.0/Backend.dll"' },
    { pid: 21, exe: "", commandLine: "/repo/src/backend/Source/bin/Debug/net10.0/Backend --urls http://localhost:5000" },
  ];
  const r = matchApiProcesses(listing, "/repo/src/backend/Source/bin", { windows: false, selfPid: 1 });
  assert.deepEqual(pids(r), [20, 21]);
});

test("another checkout's API, a test host and this process are left alone", () => {
  const listing = [
    { pid: 30, exe: "", commandLine: "/other/src/backend/Source/bin/Debug/net10.0/Backend" },
    { pid: 31, exe: "", commandLine: "dotnet exec /repo/src/backend/Tests/bin/Debug/net10.0/testhost.dll" },
    { pid: 32, exe: "", commandLine: "node scripts/stop-api.mjs /repo/src/backend/Source/bin/" },
    { pid: 33, exe: "", commandLine: "/repo/src/backend/Source/binaries/tool" },
  ];
  const r = matchApiProcesses(listing, "/repo/src/backend/Source/bin", { windows: false, selfPid: 32 });
  assert.deepEqual(pids(r), []);
});

test("case matters on POSIX and not on Windows", () => {
  const listing = [{ pid: 40, exe: "", commandLine: "/Repo/src/backend/Source/bin/Debug/Backend" }];
  assert.deepEqual(pids(matchApiProcesses(listing, "/repo/src/backend/Source/bin", { windows: false, selfPid: 1 })), []);
  assert.deepEqual(pids(matchApiProcesses(listing, "/repo/src/backend/Source/bin", { windows: true, selfPid: 1 })), [40]);
});
