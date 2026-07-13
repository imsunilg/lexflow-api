# Pilot Readiness Checklist

PRD §3 (success metrics), §8 (global ACs), §37 (UAT). This is an honest,
evidence-based snapshot of where the codebase actually stands against the
bar for putting a real pilot firm on this system — not a target-state
description. Every row below was verified by grepping the actual
`lexflow-database`/`lexflow-api`/`lexflow-web`/`lexflow-mobile` repos for
the AC/G-AC id it cites (this codebase's own established convention is to
put the AC id it satisfies in a doc comment near the implementation and,
where a test exists, in the test file/name too) — not assumed from the PRD
alone. Where no citation exists in a test file, it's marked as a gap, even
if a comment shows the feature is implemented.

**Bottom line: not pilot-ready today.** The single largest blocker is that
the demo tenant contains no business data — see §4 below — so a
non-technical reviewer cannot currently walk through any persona script
end to end. Beyond that, two of ten global ACs (G-AC6, G-AC9) have zero
automated coverage anywhere, and roughly half of the ~85 module-level ACs
are implemented but not test-traced (§2).

---

## 1. G-AC1–G-AC10 (§8 Global Acceptance Criteria)

| ID | Requirement | Status | Evidence |
|---|---|---|---|
| G-AC1 | Money integrity: Σ(invoices) − Σ(credit notes) − Σ(allocated payments) = AR everywhere; nightly job asserts it | ✅ **Passing in CI** | `AuditIntegrityCheckJob` (Infrastructure/Ops) + 3 test layers: `AuditIntegrityCheckJobTests` (unit, EF InMemory), `AuditIntegrityCheckJobIntegrationTests` (integration, real Postgres via Testcontainers), `StagingMoneyIntegrityTests` (runs the same job for real against staging — see `release.yml`) |
| G-AC2 | Any hearing/deadline write creates reminder rows in the same transaction; missing-reminders is build-failing | ✅ **Passing in CI** | `HearingService`/`HearingsController` (transactional reminder creation), `HearingChainIntegrationTests`, `AuditIntegrityCheckJob`'s retroactive sweep + its own tests |
| G-AC3 | Cross-tenant fuzz: every GET/PUT/DELETE with a foreign-tenant id returns 404/403, 100% of endpoints | ✅ **Passing in CI** | `CrossTenantFuzzFixture`/`CrossTenantFuzzTests` (`tests/LexFlow.IntegrationTests/Security`) — real HTTP via `WebApplicationFactory` |
| G-AC4 | Permission-matrix generator: (role × endpoint) grid from §21 asserts allow/deny automatically; drift fails CI | ✅ **Passing in CI** | `PermissionsMatrixGenerator` + `PermissionMatrixIntegrationTests`, persisted as `permissions_matrix.json` at repo root |
| G-AC5 | Audit log: 25-entity-type scripted scenario, zero gaps, actor/before/after/IP | ✅ **Passing in CI** | `AuditTrailIntegrationTests`, `AuditSaveChangesInterceptorTests` — interceptor-based, not per-handler opt-in |
| G-AC6 | API P95 < 300ms (read)/600ms (write) at 200 RPS/tenant; dashboard P95 < 2s; search P95 < 800ms | ❌ **No automated coverage found** | No k6 script, no perf CI job, anywhere in any of the four repos. Genuine gap — this is a load-test requirement and none exists. |
| G-AC7 | WCAG 2.1 AA, zero critical violations on top-30 screens, full keyboard operability | ✅ **Passing in CI** (partial scope) | `e2e/tests/{staff-portal,client-portal}/axe-top-screens.spec.ts` (axe-core). Keyboard-operability assertions are not separately enumerated — the axe pass covers automatically-detectable violations only, not a manual/automated full keyboard-nav sweep. |
| G-AC8 | en↔hi language switch leaves zero hardcoded strings on top-30 screens (pseudo-locale test) | 🟡 **Partially covered** | `scripts/check-i18n-coverage.mjs` (lexflow-web) — verifies every *already-extracted* `i18n`-marked string has a real Hindi translation. Its own doc comment states the scope honestly: it cannot catch static text that was never marked `i18n` in the first place, which is what a true pseudo-locale visual/DOM-scan test would need. Not implemented. |
| G-AC9 | Kill Redis / one API pod / delay Postgres 200ms — system degrades per defined behavior, no data loss | ❌ **No automated coverage found** | No chaos-engineering test, no resilience test job, anywhere. Genuine gap. |
| G-AC10 | Mobile sync property test: random offline op sequences replay to a state identical to online | 🟡 **Test exists, never executed** | `lexflow-mobile/test/sync/sync_engine_test.dart` (the AC-MB1 airplane-mode test) cites G-AC10 directly. It was authored and structurally verified (compiles, correct FIFO/exactly-once logic) but **has never actually run** — this sandbox had no Flutter/Dart SDK at any point in this engagement. `mobile-ci.yml` exists and would run it, but has never executed either (no CI run history). Also note: it's a fixed-scenario test (the specific AC-MB1 airplane-mode scenario), not a property-based/randomized test as G-AC10's own wording asks for ("random offline operation sequences") — a real gap between what's built and what's specified. |

**2 of 10 fully open (G-AC6, G-AC9); 2 of 10 partial (G-AC8, G-AC10); 6 of 10 solid.**

---

## 2. Module Acceptance Criteria traceability (§6)

Legend: ✅ automated test cites this AC id · 🟡 implemented, cited in source, but no test found citing it · ❌ no citation found anywhere (implementation status unverified by this method).

### Module 1 — Dashboard
| AC | Requirement (short) | Status | Evidence |
|---|---|---|---|
| AC-D1 | Full dashboard render < 2s on 4G, 10k-matter tenant | ❌ | No perf test found (see G-AC6). |
| AC-D2 | KPI click navigates to list with matching totals | ❌ | Not found. |
| AC-D3 | Hearing outcome elsewhere reflects on dashboard ≤5s (SignalR) | 🟡 | `dashboard-realtime.service.ts` only. |
| AC-D4 | Layout customization persists across devices | ❌ | Not found. |
| AC-D5 | Receptionist sees no revenue widgets, 403 on those endpoints | 🟡 | `widget-catalog.ts`, `widget-catalog-dialog.component.ts`, `dashboard.page.ts` — implementation only, no test. |

### Module 2 — Lead Management
| AC | Status | Evidence |
|---|---|---|
| AC-L1 (duplicate dialog <1s) | ❌ | Not found. |
| AC-L2 (Kanban drag → stage_history + notify ≤3s) | ✅ | `LeadServiceTests.cs`, `leads-kanban.page.ts` |
| AC-L3 (convert wizard atomic) | ✅ | `LeadServiceTests.cs` |
| AC-L4 (SLA breach escalation) | ❌ | Not found. |
| AC-L5 (10k-row import ≤3min) | ✅ | `LeadImportServiceTests.cs`, e2e `journey-04-leads-csv-import.spec.ts` |
| AC-L6 (lost reason required, reconciles) | 🟡 | `lost-reason-dialog.component.ts` only. |

### Module 3 — Client Management
| AC | Status | Evidence |
|---|---|---|
| AC-C1 (360° one-call summary <1.5s) | 🟡 | `ClientQueries.cs`, `ClientsController.cs` only. |
| AC-C2 (expiring KYC doc → auto-task) | 🟡 | `kyc-document-manager.component.ts` only. |
| AC-C3 (merge re-parents 100%) | ✅ | `ClientServiceTests.cs`, e2e `journey-06-client-360-contact-and-merge.spec.ts` |
| AC-C4 (delete blocked with open matter) | 🟡 | `ClientCommands.cs`, `ClientsController.cs` only. |
| AC-C5 (portal invite → client sees own data only) | ❌ | Not found by this AC id (G-AC3/AC-P1's IDOR suite is the closest real coverage, not the same assertion). |

### Module 4 — Matter Management
| AC | Status | Evidence |
|---|---|---|
| AC-M1 (conflict hit blocks until override+reason) | ✅ | `MatterServiceTests.cs` |
| AC-M2 (timeline merges 6 entity types, chronological) | 🟡 | `matters.service.ts`, `matter-activity-tab.component.ts`, `MatterQueries.cs` only. |
| AC-M3 (closing enforces checklist, read-only after) | ✅ | `MatterServiceTests.cs`, e2e `journey-09-matter-closing-checklist.spec.ts` |
| AC-M4 (financial summary = Σ invoices/payments/time) | 🟡 | `matters.service.ts`, `matter-billing-tab.component.ts`, `MatterQueries.cs` only — PRD asks for this specifically as **property-tested**; no property test found. |
| AC-M5 (important-date reminder fires per policy) | ❌ | Not found (distinct from G-AC2's reminder-row-exists check — this is about actual dispatch). |

### Module 5 — Court Case Management
| AC | Status | Evidence |
|---|---|---|
| AC-CC1 (outcome+next-date → hearing+reminders, one tx) | ✅ | `HearingServiceTests.cs`, `HearingChainIntegrationTests.cs` |
| AC-CC2 (cause-list <1s/200 hearings, prints A4) | ✅ | e2e `journey-13-cause-list-day-view.spec.ts` (print-layout assertion is e2e, not a perf assertion at 200-hearing scale — scale claim untested) |
| AC-CC3 ("no future hearing & not disposed & not sine-die" impossible) | ✅ | `AuditIntegrityCheckJobTests.cs`, integration + staging variants, `CourtCaseServiceTests.cs` |
| AC-CC4 (appeal links bidirectionally, no blob dup) | ✅ | `CourtCaseServiceTests.cs` |
| AC-CC5 (evidence custody chain append-only) | ✅ | e2e `journey-11-case-evidence-and-witnesses.spec.ts` |

### Module 6 — Calendar
| AC | Status | Evidence |
|---|---|---|
| AC-CAL1 (hearing → calendar + Google ≤60s) | 🟡 | `CalendarSyncPollingJob.cs`, `CalendarCommands.cs` only. |
| AC-CAL2 (07:00 same-day reminder, dispatch log) | ✅ | `ReminderDispatchServiceTests.cs` |
| AC-CAL3 ("this occurrence only" leaves series intact) | ✅ | `CalendarServiceTests.cs` |
| AC-CAL4 (disconnect removes/keeps events, purges tokens) | 🟡 | `ics-export-tab.component.ts`, `CalendarCommands.cs` only. |
| AC-CAL5 (1,500-item month view at 60fps) | ❌ | Not found (perf/rendering claim, see G-AC6). |

### Module 7 — Document Management
| AC | Status | Evidence |
|---|---|---|
| AC-DOC1 (scanned PDF searchable ≤60s) | 🟡 | `DocumentProcessingJobs.cs`, `ITextExtractionService.cs` only. |
| AC-DOC2 (version history: uploader/time/size/hash) | 🟡 | `DocumentService.cs`, `IDocumentService.cs`, `DocumentCommands.cs` only. |
| AC-DOC3 (privileged doc invisible in search, not just blocked) | ✅ | `DocumentServiceTests.cs` |
| AC-DOC4 (template merge incl. nested fields) | ✅ | `DocumentTemplateServiceTests.cs` |
| AC-DOC5 (expired share link → branded 410, logged) | 🟡 | `PublicDocumentShareController.cs`, `DocumentShareLinkService.cs` only. |
| AC-DOC6 (DocuSign round-trip: signed PDF + cert) | 🟡 | `SignatureWebhookController.cs`, `SignatureService.cs` only — also a real external-dependency gap noted elsewhere in this codebase (no DocuSign sandbox credentials). |

### Module 8 — Billing (incl. Trust)
| AC | Status | Evidence |
|---|---|---|
| AC-B1 (batch 200 matters ≤60s, reconciles to paisa) | ✅ | `BillingServiceTests.cs`, `FinPropertyTests.cs`, e2e `journey-23-batch-billing-wizard.spec.ts` |
| AC-B2 (GST split correct: IGST vs CGST+SGST) | ✅ | `BillingServiceTests.cs` |
| AC-B3 (Razorpay webhook ≤30s, dedup on retry) | ✅ | `PaymentServiceTests.cs` |
| AC-B4 (trust disbursement over balance impossible, incl. concurrent race) | ✅ | `TrustServiceTests.cs`, `FinPropertyTests.cs`, e2e `journey-26-trust-deposit-and-disburse.spec.ts` |
| AC-B5 (voided invoice number never reused) | ✅ | `BillingServiceTests.cs` |
| AC-B6 (aging buckets sum to total AR exactly) | ✅ | `BillingServiceTests.cs`, e2e `journey-28-aging-and-statement.spec.ts` |
| AC-B7 (DB-level UPDATE on trust_ledger_entries rejected) | 🟡 | `TrustService.cs`/`TrustLedgerEntry.cs`/`TrustController.cs` cite it; the actual enforcement is presumably a Postgres trigger/rule in `lexflow-database` — **no test (unit, integration, or DB-level) found asserting the UPDATE is actually rejected.** This is a BR-7/AC-B7 invariant with real money-safety implications and deserves a dedicated test. |

### Module 9 — Time Tracking
| AC | Status | Evidence |
|---|---|---|
| AC-T1 (timer survives browser kill + second-device login) | 🟡 | `TimeTrackingService.cs`, `RunningTimer.cs`, `TimersController.cs` — **no dedicated test file for this module at all** (no `TimeTrackingServiceTests.cs`/`TimerServiceTests.cs` exists in `tests/LexFlow.UnitTests`). |
| AC-T2 (bulk-approve 500 entries ≤5s, correct rate snapshot) | 🟡 | `approval-queue.page.ts`, `TimeEntriesController.cs` only. |
| AC-T3 (billed entry immutable via any API path) | 🟡 | `time-entries.service.ts`, `TimeEntriesController.cs` only. |
| AC-T4 (next-morning suggested entries from calendar) | ❌ | Not found. |
| AC-T5 (utilization math vs seeded fixture) | 🟡 | `utilization.page.ts` only. |

**Module 9 is the weakest-traced module in the whole codebase** — every one of its 5 ACs is either untested or has zero dedicated test file, despite AC-B3/B4 (billing/trust, which depend on time-entry data) being well covered.

### Module 10 — Task Management
| AC | Status | Evidence |
|---|---|---|
| AC-TK1 (smart-parse example → correct fields) | ✅ | `TaskServiceTests.cs` |
| AC-TK2 (blocked task can't move until predecessor Done) | ✅ | `TaskServiceTests.cs` |
| AC-TK3 (template apply computes relative dates) | ✅ | `TaskServiceTests.cs` |
| AC-TK4 (overdue escalation: owner at due, manager at +24h) | ❌ | Not found. |
| AC-TK5 (workload board counts match filtered list) | 🟡 | `TaskQueries.cs` only. |

### Module 11 — Communication
| AC | Status | Evidence |
|---|---|---|
| AC-CM1 (email to client → Gmail Sent + timeline ≤30s) | 🟡 | `GmailSyncService.cs`, `EmailCommands.cs`, `IEmailSync.cs` only. |
| AC-CM2 (WhatsApp template delivers, status webhook updates) | 🟡 | `WhatsAppCommands.cs`, `IWhatsAppProvider.cs`, controller only. |
| AC-CM3 (BCC-dropbox files to correct matter or Triage) | ✅ | `EmailServiceTests.cs` |
| AC-CM4 (DLT-off SMS blocked with clear error) | ✅ | `SmsServiceTests.cs` |
| AC-CM5 (chat delivers <500ms online; push+badge offline) | ✅ | `ChatServiceTests.cs` |

### Module 12 — Knowledge Base
| AC | Status | Evidence |
|---|---|---|
| AC-KB1 ("IPC 420" opens section <500ms cached) | ✅ | `KbCitationQueryParserTests.cs` |
| AC-KB2 (judgment full-text search, OCR snippet highlight) | 🟡 | `KbSearchService.cs`, `KbSearchQuery.cs`, controller only. |
| AC-KB3 (as-on-date view renders historical text) | ✅ | `KbActServiceTests.cs` |
| AC-KB4 (pin shows in matter Research tab + back-link count) | ✅ | `KbMatterPinServiceTests.cs` |
| AC-KB5 (unreviewed article never visible to non-authors) | ✅ | `KbArticleServiceTests.cs` |

### Module 13 — Reports & Analytics
| AC | Status | Evidence |
|---|---|---|
| AC-R1 (report totals reconcile with list-view totals) | ✅ | `StandardReportServiceTests.cs` |
| AC-R2 (3-join custom report <5s on 1M rows) | ❌ | Not found (perf/scale claim, see G-AC6). |
| AC-R3 (scheduled report arrives ≤15min, correct attachment) | 🟡 | `ReportSchedulerService.cs`, `ReportSchedule.cs` — e2e `journey-36-custom-report-schedule-run.spec.ts` exercises the schedule→run flow functionally but does not cite this AC id, and CI can't wait 15 real minutes to verify the SLA itself. |
| AC-R4 (drill-down lands on matching filtered list) | ❌ | Not found. |

### Module 14 — User Management
| AC | Status | Evidence |
|---|---|---|
| AC-U1 (deactivated user's JWTs rejected ≤60s) | ✅ | `UserManagementServiceTests.cs` |
| AC-U2 (permission inspector explains every allow) | 🟡 | `effective-permission-inspector.page.ts`, `PermissionService.cs`, `UserQueries.cs` only. |
| AC-U3 (last Owner cannot be demoted/deactivated) | ✅ | `UserManagementServiceTests.cs` |
| AC-U4 (IP-restricted role blocked+logged from foreign IP) | ❌ | Not found. |

### Module 15 — Settings
| AC | Status | Evidence |
|---|---|---|
| AC-S1 (invalid Razorpay key can't be saved) | 🟡 | `PaymentGatewayVerifier.cs`, `GatewayTestCommands.cs`, `payment-gateways.page.ts` only. |
| AC-S2 (number preview matches next generated number) | ✅ | `NumberSeriesServiceTests.cs` |
| AC-S3 (settings change in audit with diff ≤1s) | 🟡 | `SettingsQueries.cs` only. |
| AC-S4 (dark-mode logo renders on portal/PDFs) | ❌ | Not found. |

### Module 16 — AI Features
| AC | Status | Evidence |
|---|---|---|
| AC-AI1 (assistant citation link; no-access doc never retrieved) | ✅ | `AiRetrievalGuardTests.cs` |
| AC-AI2 (contract review ≥95% of 8 standard clauses) | ❌ | Not found — this is a model-quality benchmark against a seeded contract set; no such benchmark/fixture found. |
| AC-AI3 (research answers: only verifiable citations, fake-citation caught) | ✅ | `CitationVerifierTests.cs` |
| AC-AI4 (30-min Hindi-English transcription WER ≤15%) | ❌ | Not found — needs a benchmark audio set; none found. |
| AC-AI5 (AI-badge + explicit Save/Insert, no auto-apply) | ✅ | `AiGeneratedResponseTests.cs`, e2e `journey-37-ai-assistant-and-contract-review.spec.ts` |

### Module 17 — Client Portal
| AC | Status | Evidence |
|---|---|---|
| AC-P1 (portal user A can never fetch client B's resource) | ✅ | `PortalIdorTests.cs` |
| AC-P2 (outcome reflects on portal timeline ≤60s if publish-allowed) | ❌ | Not found. |
| AC-P3 (UPI payment → paid + receipt email ≤1min) | ❌ | Not found directly — D-18's `journey-02-wip-to-receipt.spec.ts` exercises the real Razorpay flow end to end but skips gracefully without live Razorpay test credentials (documented decision from that build), so it does not currently prove this AC in CI. |
| AC-P4 (client upload → correct folder + lawyer notification) | 🟡 | `PortalDocumentService.cs`, `IPortalDocumentService.cs` only. |
| AC-P5 (Lighthouse ≥90 perf/accessibility, mobile) | ❌ | Not found — no Lighthouse CI job anywhere. |

### Module 18 — Mobile Application
| AC | Status | Evidence |
|---|---|---|
| AC-MB1 (airplane-mode: outcome+scan+2 entries sync exactly once, in order) | ✅ **but never executed** | `test/sync/sync_engine_test.dart` — see G-AC10 row above for the execution caveat. |
| AC-MB2 (cold start → Today <2s, mid-range Android) | ❌ | Not found (and cannot be measured without a real device/emulator, which this whole engagement never had access to). |
| AC-MB3 (biometric required after grace; fallback = full login) | 🟡 | `biometric_gate.dart`, `login_screen.dart` — implemented, no test. |
| AC-MB4 (deactivation wipes app data at next launch) | 🟡 | `secure_token_store.dart`, `session_controller.dart` — implemented, no test. |
| AC-MB5 (scan ≤300KB/page at readable quality) | 🟡 | `scan_capture_service.dart` has real compression logic targeting this budget and logs when it can't hit it, but there's no automated test asserting the budget is met on a representative sample page. |

**Tally: of ~85 module ACs, 33 have a passing automated test citing them, ~34 are implemented but untested, and ~18 have no citation found at all (implementation status unverified by this method — may exist without following the AC-id-citation convention, or may not exist).**

---

## 3. SOC 2 Type I evidence (§33, target Month 6)

SOC 2 Type I is a point-in-time attestation that controls are *designed*
correctly — it does not require a track record of operation (that's Type
II). What follows is what technical evidence already exists in these repos
vs. what's purely organizational and cannot come from a codebase at all.

### Technical control evidence present
| Control area | Evidence |
|---|---|
| Tenant isolation | 18 RLS policy scripts (`Scripts/15_RLS_Policies/`) + G-AC3's cross-tenant fuzz suite passing in CI |
| Access control (RBAC) | §21 permission matrix, `permissions_matrix.json`, G-AC4's drift-detecting generator |
| Audit trail (immutability) | `audit.audit_events` — insert-only trigger (`003_Insert_Only_Trigger.sql`), monthly partitions, G-AC5 passing |
| Encryption in transit | HTTPS/TLS assumed at the Azure Front Door/AKS ingress layer (not something app code configures) |
| Secrets management | Azure Key Vault refs + App Configuration (per `values.yaml`'s own comment) — not committed secrets; `gitleaks` CI job scans every PR |
| Vulnerability scanning | `trivy` (container CVE scan), `zap-baseline` (OWASP baseline) — both in `api-ci.yml` |
| Change management | Every deploy gated by PR review + CI (build/unit/integration/contract/security) +, as of `release.yml`, a staging deploy + D-18/C-15 gate before production, + manual-approval `production` GitHub Environment |
| Backup/DR | §38 describes Postgres PITR (7d) + daily GRS (35d), ES snapshots, quarterly restore drills — **not verified to actually be configured**; no Bicep/Terraform/runbook found in any repo confirming this is provisioned, only the PRD's stated intent. |

### Missing / organizational (not derivable from a codebase)
- **`/ops/runbooks`** — PRD §38 explicitly names this path; it does not exist in any repo. No incident-response, breach-notification (DPDP 72h), or restore-drill runbooks exist anywhere.
- Formal risk assessment, vendor/subprocessor due-diligence records (DPAs), and a RoPA (GDPR) — none exist; these are compliance-team deliverables, not code.
- Employee background-check policy, security-awareness training records, access-review cadence evidence — organizational, not in-repo.
- A named auditor/engagement letter for the actual SOC 2 Type I assessment — none referenced anywhere.
- VAPT (quarterly penetration test) report — `zap-baseline` is an automated OWASP baseline scan, not a penetration test; no third-party VAPT report exists.
- DLT registration confirmation (India SMS) and WhatsApp Business template-approval records — `AC-CM4`'s test proves the *code* blocks non-compliant sends; it doesn't prove the firm's actual DLT registration exists.

**Verdict: the technical-control half is in reasonable shape for a Type I design assessment; the organizational/evidentiary half (runbooks, DPAs, training records, an actual engagement with an auditor) has not been started anywhere in these repos, which is expected — that's not something four codebases can produce on their own.**

---

## 4. Seed demo tenant — walkthrough-ability (§37, §4 personas)

**This is the most significant pilot-blocking gap found in this review.**

PRD §37 calls for a "seed factory (`LexFlow.Seeder`) producing deterministic
demo tenant (200 clients, 1k matters, 5k hearings, 10k entries) used by all
test tiers and demos." **`LexFlow.Seeder` does not exist.** This isn't an
inference — `lexflow-database/Scripts/16_Seed/008_Demo_Tenant.sql`'s own
comment says so directly: it seeds *only* one tenant + one branch, "matching
the seed factory's demo tenant id so that seeder can attach its generated
data to the same tenant/branch" — a seeder that was never built.

What actually exists in the demo tenant (`lexflow-demo`, id
`00000000-0000-0000-0000-000000000001`) today:
- 1 tenant, 1 branch ("Head Office"), reference data only (permissions
  catalog, system roles, India courts/tribunals, practice areas, activity
  codes, GST tax configs, lost reasons) — all from `16_Seed/001`–`007`.
- 1 login-capable staff user + 1 portal-enabled client + 1 number series
  (`INV`), from `lexflow-api/tools/E2eSeed` — built for automated E2E tests,
  not for a human demo (default password, no realistic name/history).
- **Zero leads, zero real clients beyond the one E2eSeed portal client,
  zero matters, zero court cases, zero hearings, zero invoices, zero
  documents, zero tasks, zero KB content.**

### Persona-script walkthrough-ability (§4)

| Persona | Can a non-technical reviewer walk this today? | Why / why not |
|---|---|---|
| P-1 Advocate Arjun (solo litigator, mobile) | ❌ | No matters/hearings exist to show a hearing reminder or outcome-capture flow against; mobile app has never been run at all (no Flutter toolchain anywhere in this engagement). |
| P-2 Managing Partner Meera (45-lawyer firm dashboards) | ❌ | Dashboard widgets (utilization, realization, revenue) have nothing to aggregate — one tenant, no lawyers-with-workload, no invoices. |
| P-3 Associate Aditi (tasks, timers, AI drafting) | ❌ | No tasks or matters exist to attach a timer/checklist to. |
| P-4 Paralegal Priya (calendar, OCR/search, bulk import) | 🟡 partial | The lead-import wizard (AC-L5) can be demoed by uploading a CSV live; there's nothing pre-loaded to show a *populated* calendar or document search. |
| P-5 Receptionist Rakesh (create leads, log calls) | 🟡 partial | Lead quick-capture works standalone (no dependent data needed) — the one persona script that's actually walkthrough-able as-is. |
| P-6 Finance Head Farah (billing, GST, trust, aging) | ❌ | No invoices/payments/trust ledger entries exist; the aging report and trust ledger would render empty. |
| P-7 Corporate Counsel Chetan (budgets vs actuals) | ❌ | No matter budget data seeded. |
| P-8 Client Kavita (portal: timeline, invoices, messages) | 🟡 partial | The one E2eSeed portal client can log in, but has no matter/timeline/invoice to look at — proves login works, not the persona's actual "wants status without calling" story. |
| P-9 HR Manager Hina (user lifecycle, login history) | 🟡 partial | User CRUD/roles are demoable (reference data is real); login-history/session data is sparse since almost nothing has logged in. |
| P-10 Sysadmin Sam (SMTP/SMS/WhatsApp config, workflow rules) | ✅ | Settings/config screens don't depend on business data — genuinely demoable now. |

**0 of 10 personas fully walkthrough-able; 4 partial; 6 blocked.** Building
`LexFlow.Seeder` (or an equivalent demo-data generator producing a
realistic small-to-medium firm — a few dozen clients/matters/hearings is
enough for a *pilot demo*; the PRD's 200/1k/5k/10k figures are sized for
*load testing*, not a walkthrough) is the single highest-leverage thing to
do before any pilot-readiness review can move past this section.

---

## 5. Summary punch list (in priority order)

1. **Build a demo-data seeder** (a scaled-down `LexFlow.Seeder` — even a
   few dozen clients/matters/hearings/invoices is enough) so §4's persona
   scripts are actually walkthrough-able. Nothing else in this document
   matters to a pilot firm if there's nothing to click through.
2. **G-AC6 (perf) and G-AC9 (resilience)** have zero coverage — at minimum
   a k6 smoke script against staging and a documented chaos-drill runbook.
3. **Module 9 (Time Tracking)** has no dedicated test file at all — the
   highest-density gap of any single module.
4. **AC-B7** (DB-level trust-ledger UPDATE rejection) has no test verifying
   a real money-safety invariant — should be one of the first gaps closed.
5. **`/ops/runbooks`** doesn't exist — needed for SOC 2 Type I regardless
   of pilot timing (breach notification, restore drills).
6. Actually execute `mobile-ci.yml` and the D-18/C-15 staging gate at least
   once each — both are wired up and believed-correct but have no run
   history in this engagement (no Flutter SDK, no live AKS/Azure
   environment were ever available to verify against).
7. Decide whether the ~34 "implemented, no test" module ACs need dedicated
   tests before pilot, or whether broader e2e/journey coverage already
   exercises them well enough in practice — this document only checked for
   explicit AC-id citations, which is a real but conservative signal.
