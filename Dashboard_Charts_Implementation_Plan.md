# Dashboard Charts Implementation Plan

## Goal

Add 2 charts to both dashboards:

### Admin Dashboard

- Pie Chart - Label: `توزيع حالات الشركة`
- Bar Chart - Multiple: `تسجيل الشركات` with weekly data

### Company Dashboard

- Pie Chart - Label: `توزيع الوظائف`
- Bar Chart - Multiple: `إحصائيات التقديمات` with weekly data

---

## 1. Backend Plan

### A. Create reusable chart DTOs

Create general DTOs so frontend can reuse the same shadcn components without page-specific mapping hacks.

Suggested DTOs:

- `ChartPieItemDTO`
  - `key`
  - `label`
  - `value`
  - `fill`

- `ChartPieResponseDTO`
  - `title`
  - `description`
  - `items`

- `ChartSeriesDTO`
  - `key`
  - `label`
  - `color`

- `ChartBarMultiPointDTO`
  - `label`
  - dynamic numeric fields for each series key
  - practical option in C#:
  - use explicit fields for each chart endpoint instead of a fully generic object

- `ChartBarMultiResponseDTO`
  - `title`
  - `description`
  - `period`
  - `xKey`
  - `series`
  - `items`

Because this project is strongly typed, I recommend:

- keep the pie response reusable
- make bar chart response DTOs specific per endpoint

That will be simpler and cleaner.

---

### B. Admin dashboard chart endpoints

Put these inside `AdminController`.

#### 1. Pie chart endpoint

- `GET /api/Admin/dashboard-charts/company-status-distribution`

Purpose:

- return distribution of company statuses

Data source:

- `TbEmployers`
- group by `EmployerStatusId`

Suggested returned items:

- `موثقة`
- `قيد المراجعة`
- `مرفوضة`
- optionally `معلقة` or `محظورة` if you want them shown

Response example:

```json
{
  "statusCode": 200,
  "data": {
    "title": "توزيع حالات الشركة",
    "description": "التوزيع الحالي",
    "items": [
      { "key": "verified", "label": "موثقة", "value": 195, "fill": "var(--chart-1)" },
      { "key": "pending", "label": "قيد المراجعة", "value": 34, "fill": "var(--chart-2)" },
      { "key": "rejected", "label": "مرفوضة", "value": 18, "fill": "var(--chart-3)" }
    ]
  }
}
```

#### 2. Weekly bar chart endpoint

- `GET /api/Admin/dashboard-charts/company-registrations?period=weekly`

Purpose:

- weekly company registration activity

Since you want `Bar Chart - Multiple`, it should return two series, not one.

Best pair:

- `companiesRegistered`
- `pendingVerifications`

Why:

- both are admin-relevant
- both are derived from company data
- much better than fake categories

Response example:

```json
{
  "statusCode": 200,
  "data": {
    "title": "تسجيل الشركات",
    "description": "آخر 7 أيام",
    "period": "weekly",
    "xKey": "label",
    "series": [
      { "key": "companiesRegistered", "label": "الشركات المسجلة", "color": "var(--chart-1)" },
      { "key": "pendingVerifications", "label": "طلبات التوثيق", "color": "var(--chart-2)" }
    ],
    "items": [
      { "label": "السبت", "companiesRegistered": 2, "pendingVerifications": 1 },
      { "label": "الأحد", "companiesRegistered": 1, "pendingVerifications": 1 },
      { "label": "الاثنين", "companiesRegistered": 3, "pendingVerifications": 2 },
      { "label": "الثلاثاء", "companiesRegistered": 2, "pendingVerifications": 1 },
      { "label": "الأربعاء", "companiesRegistered": 4, "pendingVerifications": 3 },
      { "label": "الخميس", "companiesRegistered": 1, "pendingVerifications": 1 },
      { "label": "الجمعة", "companiesRegistered": 2, "pendingVerifications": 0 }
    ]
  }
}
```

---

### C. Company dashboard chart endpoints

Put these inside `CompanyController`.

#### 1. Pie chart endpoint

- `GET /api/Company/dashboard-charts/job-distribution`

Purpose:

- return distribution of jobs for current logged-in company

Important:

For pie chart, choose one dimension only.

Best choice:

- `نشطة`
- `منتهية`
- `مغلقة`
- `مكتملة`

Response example:

```json
{
  "statusCode": 200,
  "data": {
    "title": "توزيع الوظائف",
    "description": "التوزيع الحالي",
    "items": [
      { "key": "active", "label": "نشطة", "value": 24, "fill": "var(--chart-1)" },
      { "key": "expired", "label": "منتهية", "value": 8, "fill": "var(--chart-2)" },
      { "key": "closed", "label": "مغلقة", "value": 5, "fill": "var(--chart-3)" },
      { "key": "filled", "label": "مكتملة", "value": 3, "fill": "var(--chart-4)" }
    ]
  }
}
```

#### 2. Weekly bar chart endpoint

- `GET /api/Company/dashboard-charts/applications-trend?period=weekly`

Purpose:

- weekly application activity for current company

Since you want `Bar Chart - Multiple`, best pair is:

- `applications`
- `interviews`

Why:

- both are weekly operational signals
- matches company hiring activity
- gives a much better chart than a single-series chart

Response example:

```json
{
  "statusCode": 200,
  "data": {
    "title": "إحصائيات التقديمات",
    "description": "آخر 7 أيام",
    "period": "weekly",
    "xKey": "label",
    "series": [
      { "key": "applications", "label": "التقديمات", "color": "var(--chart-1)" },
      { "key": "interviews", "label": "المقابلات", "color": "var(--chart-2)" }
    ],
    "items": [
      { "label": "السبت", "applications": 4, "interviews": 1 },
      { "label": "الأحد", "applications": 6, "interviews": 2 },
      { "label": "الاثنين", "applications": 3, "interviews": 1 },
      { "label": "الثلاثاء", "applications": 8, "interviews": 3 },
      { "label": "الأربعاء", "applications": 5, "interviews": 2 },
      { "label": "الخميس", "applications": 7, "interviews": 2 },
      { "label": "الجمعة", "applications": 2, "interviews": 1 }
    ]
  }
}
```

---

### D. Weekly grouping rules

Use the same weekly logic in both dashboards:

- return exactly 7 points
- each point = one day
- labels in Arabic:
  - `السبت`
  - `الأحد`
  - `الاثنين`
  - `الثلاثاء`
  - `الأربعاء`
  - `الخميس`
  - `الجمعة`

Recommended backend logic:

- define `today = DateTime.UtcNow.Date`
- build the last 7 calendar days
- aggregate per day
- return zero for missing days

This is important so frontend always receives fixed-length chart data.

---

### E. Backend implementation order

1. Add shared pie chart DTOs
2. Add admin chart DTOs for multiple bar chart
3. Add company chart DTOs for multiple bar chart
4. Add admin service methods
5. Add company service/controller methods
6. Add controller endpoints
7. Add `.md` docs for the 4 chart endpoints

---

## 2. Frontend Plan

### A. Use shadcn chart components directly

Frontend should keep using:

- `ChartPieLabel`
- `ChartBarMultiple`

But convert them into reusable app components that accept API data as props.

Do not hardcode:

- `chartData`
- `chartConfig`
- `title`
- `description`
- `footer text`

Instead pass them in as props.

---

### B. Create reusable frontend wrappers

#### 1. Reusable Pie component

Example concept:

- `DashboardPieChart`

Props:

- `title`
- `description`
- `data`
- `config`
- optional footer text

It should still use the shadcn structure:

- `ChartContainer`
- `ChartTooltip`
- `ChartTooltipContent`
- `Pie`
- `PieChart`

Expected transformed data:

```ts
[
  { key: "verified", label: "موثقة", value: 195, fill: "var(--chart-1)" }
]
```

Frontend mapping for component:

- `dataKey="value"`
- `nameKey="label"`

#### 2. Reusable Multiple Bar component

Example concept:

- `DashboardBarMultipleChart`

Props:

- `title`
- `description`
- `data`
- `config`
- `xKey`
- `barKeys`

It should still use:

- `BarChart`
- `CartesianGrid`
- `XAxis`
- `ChartTooltip`
- `Bar`

---

### C. Frontend page integration

#### Admin dashboard

Fetch:

- `/api/Admin/dashboard-charts/company-status-distribution`
- `/api/Admin/dashboard-charts/company-registrations?period=weekly`

Render:

- `DashboardPieChart` with company status distribution
- `DashboardBarMultipleChart` with weekly company registrations

#### Company dashboard

Fetch:

- `/api/Company/dashboard-charts/job-distribution`
- `/api/Company/dashboard-charts/applications-trend?period=weekly`

Render:

- `DashboardPieChart` with job distribution
- `DashboardBarMultipleChart` with weekly applications/interviews

---

### D. Frontend transform rules

#### Pie chart transform

Backend:

```ts
items: [{ key, label, value, fill }]
```

Frontend chart data:

```ts
items.map(item => ({
  label: item.label,
  value: item.value,
  fill: item.fill,
}))
```

Frontend chart config:

```ts
const chartConfig = {
  value: { label: "العدد" },
  verified: { label: "موثقة", color: "var(--chart-1)" },
  pending: { label: "قيد المراجعة", color: "var(--chart-2)" },
}
```

#### Bar chart transform

Backend already returns:

- `series`
- `items`

Frontend chart config:

```ts
const chartConfig = {
  applications: {
    label: "التقديمات",
    color: "var(--chart-1)",
  },
  interviews: {
    label: "المقابلات",
    color: "var(--chart-2)",
  },
}
```

---

### E. UI notes for frontend

- Keep the shadcn components unchanged structurally
- Only make them prop-driven
- Use Arabic titles/descriptions from API or from page constants
- Keep both charts same height for dashboard balance
- For weekly chart, do not slice labels to 3 chars if Arabic day names become unclear
- Use full labels or short Arabic labels like:
  - `سبت`
  - `أحد`
  - `اثن`
  - `ثلا`
  - `أرب`
  - `خمي`
  - `جمع`

---

## 3. Final endpoint list

### Admin

- `GET /api/Admin/dashboard-charts/company-status-distribution`
- `GET /api/Admin/dashboard-charts/company-registrations?period=weekly`

### Company

- `GET /api/Company/dashboard-charts/job-distribution`
- `GET /api/Company/dashboard-charts/applications-trend?period=weekly`

---

## 4. Recommended final chart meanings

### Admin Dashboard

- Pie: `توزيع حالات الشركة`
  - `موثقة`
  - `قيد المراجعة`
  - `مرفوضة`

- Bar Multiple: `تسجيل الشركات`
  - `الشركات المسجلة`
  - `طلبات التوثيق`

### Company Dashboard

- Pie: `توزيع الوظائف`
  - `نشطة`
  - `منتهية`
  - `مغلقة`
  - `مكتملة`

- Bar Multiple: `إحصائيات التقديمات`
  - `التقديمات`
  - `المقابلات`

---

## 5. Suggested implementation sequence

1. Confirm final chart labels and series names
2. Implement backend DTOs and 4 endpoints
3. Add `.md` API docs for chart endpoints
4. Refactor frontend shadcn examples into reusable chart wrappers
5. Connect admin dashboard
6. Connect company dashboard
7. Test weekly labels and empty-state handling
