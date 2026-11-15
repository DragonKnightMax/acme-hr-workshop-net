import { test } from "node:test";
import assert from "node:assert/strict";
import { leaveOn, monthGrid } from "./calendar.ts";
import type { LeaveRequest } from "./api";

test("monthGrid starts on Monday and pads both ends", () => {
  const g = monthGrid(2026, 10); // Nov 2026: 1st is a Sunday, 30 days
  assert.deepEqual(g[0], [null, null, null, null, null, null, "2026-11-01"]);
  assert.deepEqual(g.at(-1), ["2026-11-30", null, null, null, null, null, null]);
  assert.equal(g.length, 6);
});

test("monthGrid handles a leap February", () => {
  assert.equal(monthGrid(2028, 1).flat().filter(Boolean).at(-1), "2028-02-29");
});

const req = (o: Partial<LeaveRequest>): LeaveRequest =>
  ({ id: 1, employeeId: 1, startDate: "2026-11-13", endDate: "2026-11-17", halfDay: "None", days: 3, status: "Approved", reason: "", ...o });

test("leaveOn includes start and end, skips weekends and Rejected", () => {
  const rows = [req({}), req({ id: 2, status: "Rejected" }), req({ id: 3, status: "Pending" })];
  assert.deepEqual(leaveOn(rows, "2026-11-13").map(r => r.id), [1, 3]);
  assert.deepEqual(leaveOn(rows, "2026-11-17").map(r => r.id), [1, 3]);
  assert.deepEqual(leaveOn(rows, "2026-11-14"), []); // Saturday
  assert.deepEqual(leaveOn(rows, "2026-11-18"), []);
});
