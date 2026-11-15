"use client";
import { useEffect, useState } from "react";
import { api, Employee, LeaveRequest } from "@/lib/api";
import { leaveOn, monthGrid } from "@/lib/calendar";

export default function Calendar() {
  const now = new Date();
  const [month, setMonth] = useState({ y: now.getFullYear(), m: now.getMonth() });
  const [rows, setRows] = useState<LeaveRequest[]>([]);
  const [names, setNames] = useState<Record<number, string>>({});
  const [error, setError] = useState("");

  // The API only returns what this user may see (own, team or all), so the calendar follows the same rules.
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    Promise.all([api<LeaveRequest[]>("/leave"), api<Employee[]>("/employees")])
      .then(([ls, es]) => { setRows(ls); setNames(Object.fromEntries(es.map(e => [e.id, e.name]))); })
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  const shift = (d: number) => setMonth(({ y, m }) => ({ y: y + Math.floor((m + d) / 12), m: (m + d + 12) % 12 }));
  const title = new Date(month.y, month.m, 1).toLocaleDateString("en-MY", { month: "long", year: "numeric" });

  return (
    <div className="card">
      <h2>
        <button type="button" onClick={() => shift(-1)} aria-label="Previous month">‹</button>{" "}
        {title}{" "}
        <button type="button" onClick={() => shift(1)} aria-label="Next month">›</button>
      </h2>
      {loading && <p>Loading…</p>}
      {error && <p className="error">{error}</p>}
      <table className="calendar">
        <thead><tr>{["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map(d => <th key={d}>{d}</th>)}</tr></thead>
        <tbody>{monthGrid(month.y, month.m).map((week, w) => (
          <tr key={w}>{week.map((date, i) => (
            <td key={i}>{date && <>
              <div className="day">{Number(date.slice(8))}</div>
              {leaveOn(rows, date).map(r => (
                <div key={r.id} className={r.status === "Pending" ? "pending" : undefined} title={r.status}>
                  {names[r.employeeId] ?? `#${r.employeeId}`}{r.halfDay !== "None" && ` (${r.halfDay})`}
                </div>
              ))}
            </>}</td>
          ))}</tr>
        ))}</tbody>
      </table>
    </div>
  );
}
