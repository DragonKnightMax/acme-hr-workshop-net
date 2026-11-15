"use client";
import { FormEvent, useEffect, useState } from "react";
import { api, Balance, currentEmployeeId, LeaveRequest } from "@/lib/api";

export default function Leave() {
  const [rows, setRows] = useState<LeaveRequest[]>([]);
  const [balance, setBalance] = useState<Balance | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({ startDate: "", endDate: "", halfDay: "None", reason: "" });

  async function load() {
    try {
      const [list, bal] = await Promise.all([
        api<LeaveRequest[]>("/leave"),
        api<Balance>(`/employees/${currentEmployeeId()}/balance`),
      ]);
      setRows(list);
      setBalance(bal);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => { load(); }, []);

  async function run(action: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await action();
      await load();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function create(e: FormEvent) {
    e.preventDefault();
    // A half day is a single date, so the end date follows the start date.
    const body = form.halfDay === "None" ? form : { ...form, endDate: form.startDate };
    run(async () => {
      await api("/leave", { method: "POST", body: JSON.stringify(body) });
      setForm({ startDate: "", endDate: "", halfDay: "None", reason: "" });
    });
  }

  const decide = (id: number, action: "approve" | "reject") =>
    run(() => api(`/leave/${id}/${action}`, { method: "POST" }));

  const halfDay = form.halfDay !== "None";
  return (
    <div className="card">
      <h2>Leave requests</h2>
      {balance && <p>Available balance: <strong>{balance.available}</strong> of {balance.entitlement} days ({balance.pending} pending)</p>}
      {error && <p className="error" role="alert">{error}</p>}

      <form onSubmit={create}>
        <label>From <input type="date" required value={form.startDate} onChange={e => setForm({ ...form, startDate: e.target.value })} /></label>{" "}
        <label>To <input type="date" required={!halfDay} disabled={halfDay} value={halfDay ? form.startDate : form.endDate} onChange={e => setForm({ ...form, endDate: e.target.value })} /></label>{" "}
        <label>Half day{" "}
          <select value={form.halfDay} onChange={e => setForm({ ...form, halfDay: e.target.value })}>
            <option value="None">No (full days)</option>
            <option value="AM">AM</option>
            <option value="PM">PM</option>
          </select>
        </label>{" "}
        <label>Reason <input value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })} /></label>{" "}
        <button type="submit" disabled={busy}>{busy ? "Saving…" : "Request leave"}</button>
      </form>

      {loading ? <p>Loading…</p> : (
        <table><thead><tr><th>ID</th><th>Employee</th><th>From</th><th>To</th><th>Half day</th><th>Days</th><th>Status</th><th>Reason</th><th></th></tr></thead>
          <tbody>{rows.map(r => <tr key={r.id}>
            <td>{r.id}</td><td>{r.employeeId}</td><td>{r.startDate}</td><td>{r.endDate}</td><td>{r.halfDay}</td><td>{r.days}</td><td>{r.status}</td><td>{r.reason}</td>
            <td>{r.status === "Pending" && <>
              <button disabled={busy} onClick={() => decide(r.id, "approve")}>Approve</button>{" "}
              <button disabled={busy} onClick={() => decide(r.id, "reject")}>Reject</button>
            </>}</td>
          </tr>)}</tbody>
        </table>
      )}
    </div>
  );
}
