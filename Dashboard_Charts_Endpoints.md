# Dashboard Charts API Documentation

## Overview

This document describes the chart endpoints used in:

- `لوحة التحكم` for `Admin`
- `لوحة التحكم` for `Company`

The frontend will use:

- `Pie Chart - Label` from shadcn charts
- `Bar Chart - Multiple` from shadcn charts

All bar chart endpoints in this document currently support **weekly** data only.

---

## Frontend Component Mapping

### Pie Chart - Label

This component should be used for:

- `توزيع حالات الشركة` in Admin dashboard
- `توزيع الوظائف` in Company dashboard

Suggested mapping:

- `data` = `response.data.items`
- `dataKey` = `value`
- `nameKey` = `label`

### Bar Chart - Multiple

This component should be used for:

- `تسجيل الشركات` in Admin dashboard
- `إحصائيات التقديمات` in Company dashboard

Suggested mapping:

- `data` = `response.data.items`
- `XAxis dataKey` = `label`
- `Bar dataKey` = keys defined in `response.data.series`

---

## 1. Admin Pie Chart

### Company Status Distribution

This endpoint returns pie chart data for `توزيع حالات الشركة`.

**Method:** `GET`  
**URL:** `/api/Admin/dashboard-charts/company-status-distribution`

### Authorization

- `Admin`
- `SubAdmin`

### Query Parameters

This endpoint does not accept any query parameters.

### Response Shape

```json
{
  "statusCode": 200,
  "data": {
    "title": "توزيع حالات الشركة",
    "description": "التوزيع الحالي",
    "items": [
      {
        "key": "active",
        "label": "موثقة",
        "value": 195,
        "fill": "var(--chart-1)"
      },
      {
        "key": "pending",
        "label": "قيد المراجعة",
        "value": 34,
        "fill": "var(--chart-2)"
      },
      {
        "key": "rejected",
        "label": "مرفوضة",
        "value": 18,
        "fill": "var(--chart-3)"
      },
      {
        "key": "suspended",
        "label": "موقوفة",
        "value": 7,
        "fill": "var(--chart-4)"
      }
    ]
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `title` | string | Chart title. |
| `description` | string | Chart subtitle or description. |
| `items` | array | Pie chart segments. |
| `items[].key` | string | Internal frontend key. |
| `items[].label` | string | Arabic display label for the pie segment. |
| `items[].value` | int | Numeric value for the pie segment. |
| `items[].fill` | string | CSS color token used directly by the chart. |

### Intended Meaning

- `موثقة` = companies with active/approved status
- `قيد المراجعة` = companies pending verification
- `مرفوضة` = rejected companies
- `موقوفة` = suspended companies

---

## 2. Admin Multiple Bar Chart

### Company Registrations

This endpoint returns weekly multiple-bar chart data for `تسجيل الشركات`.

**Method:** `GET`  
**URL:** `/api/Admin/dashboard-charts/company-registrations?period=weekly`

### Authorization

- `Admin`
- `SubAdmin`

### Query Parameters

| Parameter | Type | Required | Description |
|---|---|---|---|
| `period` | string | Yes | Currently must be `weekly`. |

### Response Shape

```json
{
  "statusCode": 200,
  "data": {
    "title": "تسجيل الشركات",
    "description": "آخر 7 أيام",
    "period": "weekly",
    "xKey": "label",
    "series": [
      {
        "key": "companiesRegistered",
        "label": "الشركات المسجلة",
        "color": "var(--chart-1)"
      },
      {
        "key": "pendingVerifications",
        "label": "طلبات التوثيق",
        "color": "var(--chart-2)"
      }
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
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `title` | string | Chart title. |
| `description` | string | Chart subtitle or description. |
| `period` | string | Currently always `weekly`. |
| `xKey` | string | X-axis field name. |
| `series` | array | Describes the bars/legend. |
| `series[].key` | string | Data key used by the bar chart. |
| `series[].label` | string | Arabic legend label. |
| `series[].color` | string | CSS color token used by the chart. |
| `items` | array | Weekly chart points. |
| `items[].label` | string | Arabic day label. |
| `items[].companiesRegistered` | int | Companies created on that day. |
| `items[].pendingVerifications` | int | Pending verification companies created on that day. |

### Business Notes

- This endpoint returns exactly 7 points.
- Each point represents one day in the current week.
- Missing days should still appear with value `0`.

---

## 3. Company Pie Chart

### Job Distribution

This endpoint returns pie chart data for `توزيع الوظائف`.

**Method:** `GET`  
**URL:** `/api/Company/dashboard-charts/job-distribution`

### Authorization

- `Company`
- `Admin`

### Query Parameters

This endpoint does not accept any query parameters.

### Response Shape

```json
{
  "statusCode": 200,
  "data": {
    "title": "توزيع الوظائف",
    "description": "التوزيع الحالي",
    "items": [
      {
        "key": "active",
        "label": "نشطة",
        "value": 24,
        "fill": "var(--chart-1)"
      },
      {
        "key": "expired",
        "label": "منتهية",
        "value": 8,
        "fill": "var(--chart-2)"
      },
      {
        "key": "closed",
        "label": "مغلقة",
        "value": 5,
        "fill": "var(--chart-3)"
      },
      {
        "key": "filled",
        "label": "مكتملة",
        "value": 3,
        "fill": "var(--chart-4)"
      }
    ]
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `title` | string | Chart title. |
| `description` | string | Chart subtitle or description. |
| `items` | array | Pie chart segments. |
| `items[].key` | string | Internal frontend key. |
| `items[].label` | string | Arabic display label. |
| `items[].value` | int | Numeric value. |
| `items[].fill` | string | CSS color token used directly by the chart. |

### Intended Meaning

- `نشطة` = published and not expired jobs
- `منتهية` = expired jobs
- `مغلقة` = manually closed jobs
- `مكتملة` = filled jobs

---

## 4. Company Multiple Bar Chart

### Applications Trend

This endpoint returns weekly multiple-bar chart data for `إحصائيات التقديمات`.

**Method:** `GET`  
**URL:** `/api/Company/dashboard-charts/applications-trend?period=weekly`

### Authorization

- `Company`
- `Admin`

### Query Parameters

| Parameter | Type | Required | Description |
|---|---|---|---|
| `period` | string | Yes | Currently must be `weekly`. |

### Response Shape

```json
{
  "statusCode": 200,
  "data": {
    "title": "إحصائيات التقديمات",
    "description": "آخر 7 أيام",
    "period": "weekly",
    "xKey": "label",
    "series": [
      {
        "key": "applications",
        "label": "التقديمات",
        "color": "var(--chart-1)"
      },
      {
        "key": "interviews",
        "label": "المقابلات",
        "color": "var(--chart-2)"
      }
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
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `title` | string | Chart title. |
| `description` | string | Chart subtitle or description. |
| `period` | string | Currently always `weekly`. |
| `xKey` | string | X-axis field name. |
| `series` | array | Describes the bars/legend. |
| `series[].key` | string | Data key used by the bar chart. |
| `series[].label` | string | Arabic legend label. |
| `series[].color` | string | CSS color token used by the chart. |
| `items` | array | Weekly chart points. |
| `items[].label` | string | Arabic day label. |
| `items[].applications` | int | Applications created on that day for the current company. |
| `items[].interviews` | int | Interviews created on that day for the current company. |

### Business Notes

- This endpoint returns exactly 7 points.
- Each point represents one day in the current week.
- Missing days should still appear with value `0`.

---

## Frontend Integration Notes

- Use the shadcn chart components directly.
- Do not hardcode data in the components.
- Feed `response.data.items` into the chart data source.
- Feed `response.data.series` into the chart config for the multiple bar chart.
- For the pie chart:
  - use `dataKey="value"`
  - use `nameKey="label"`
- For the multiple bar chart:
  - use `XAxis dataKey="label"`
  - render one `Bar` per item in `series`
- The backend already returns color tokens like `var(--chart-1)` and `var(--chart-2)` for direct use with shadcn charts.

---

## Suggested Requests

### Admin Dashboard

```http
GET /api/Admin/dashboard-charts/company-status-distribution
```

```http
GET /api/Admin/dashboard-charts/company-registrations?period=weekly
```

### Company Dashboard

```http
GET /api/Company/dashboard-charts/job-distribution
```

```http
GET /api/Company/dashboard-charts/applications-trend?period=weekly
```

---

## Notes For Another Model

If another model is integrating these dashboards, it should assume:

- the pie chart endpoints are for snapshot distribution only
- the multiple bar chart endpoints are weekly only for now
- the frontend should continue using shadcn chart components, not replace them
- the backend responses are shaped to be consumed with minimal transformation
- all 4 endpoints are separate and should be called independently depending on the dashboard and chart
