import type { LeaveRequest } from "./api";

// Dates stay as "YYYY-MM-DD" strings (what the API sends), so string compare = date compare, no time zones.
const iso = (y: number, m: number, d: number) => `${y}-${String(m + 1).padStart(2, "0")}-${String(d).padStart(2, "0")}`;

/** Weeks of the month (m is 0-based), Monday first; days outside the month are null. */
export function monthGrid(y: number, m: number): (string | null)[][] {
  const lead = (new Date(y, m, 1).getDay() + 6) % 7; // Mon = 0
  const days = new Date(y, m + 1, 0).getDate();
  const cells: (string | null)[] = [...Array(lead).fill(null), ...Array.from({ length: days }, (_, i) => iso(y, m, i + 1))];
  while (cells.length % 7) cells.push(null);
  return Array.from({ length: cells.length / 7 }, (_, w) => cells.slice(w * 7, w * 7 + 7));
}

/** Approved and Pending requests covering this date. Weekends are skipped: leave counts working days only. */
export function leaveOn(rows: LeaveRequest[], date: string): LeaveRequest[] {
  const dow = new Date(date + "T00:00:00").getDay();
  if (dow === 0 || dow === 6) return [];
  return rows.filter(r => r.status !== "Rejected" && r.startDate <= date && date <= r.endDate);
}
