# Company Page Statistics API Documentation

## Overview

This document describes the statistics endpoints used by the company dashboard pages.

Each page has its own dedicated endpoint for the top cards, so the frontend should call the endpoint that belongs to that specific page.

The covered pages are:

1. `إدارة الوظائف`
2. `طلبات التوظيف`
3. `سجل التوظيف`
4. `المقابلات`
5. `لوحة التحكم`

All endpoints in this document are intended for authenticated company users.

---

## 1. Jobs Page Statistics

This endpoint is for the `إدارة الوظائف` page cards.

**Method:** `GET`  
**URL:** `/api/Jobs/jobs/statistics`

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
    "totalJobs": 12,
    "activeJobs": 7,
    "expiredJobs": 3,
    "fullTimeJobs": 8,
    "partTimeJobs": 2
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `totalJobs` | int | Total number of jobs belonging to the current logged-in company. |
| `activeJobs` | int | Jobs with published status and not expired yet. |
| `expiredJobs` | int | Jobs that are closed, filled, expired, or past expiration date. |
| `fullTimeJobs` | int | Jobs with job type `FullTime`. |
| `partTimeJobs` | int | Jobs with job type `PartTime`. |

### Card Mapping

| Card Label | Field |
|---|---|
| `إجمالي الوظائف` | `totalJobs` |
| `الوظائف النشطة` | `activeJobs` |
| `الوظائف المنتهية` | `expiredJobs` |
| `وظائف بدوام كامل` | `fullTimeJobs` |
| `وظائف بدوام جزئي` | `partTimeJobs` |

---

## 2. Applications Page Statistics

This endpoint is for the `طلبات التوظيف` page cards.

**Method:** `GET`  
**URL:** `/api/Applications/company/statistics`

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
    "totalApplications": 34,
    "pendingReviewApplications": 9,
    "shortlistedApplications": 11,
    "interviewedApplications": 5,
    "applicationsThisWeek": 7
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `totalApplications` | int | Total active applications for the current company. |
| `pendingReviewApplications` | int | Applications in `PendingReview` status. |
| `shortlistedApplications` | int | Applications in `Shortlisted` status. |
| `interviewedApplications` | int | Applications in `Interviewed` status. |
| `applicationsThisWeek` | int | Active applications created during the current week. |

### Card Mapping

| Card Label | Field |
|---|---|
| `إجمالي الطلبات` | `totalApplications` |
| `قيد الانتظار` | `pendingReviewApplications` |
| `بانتظار المقابلة` | `shortlistedApplications` |
| `تمت المقابلة` | `interviewedApplications` |
| `طلبات هذا الأسبوع` | `applicationsThisWeek` |

### Business Note

This endpoint is for the active applications page only. It is separate from the historical employment records page.

---

## 3. Employment Records Page Statistics

This endpoint is for the `سجل التوظيف` page cards.

**Method:** `GET`  
**URL:** `/api/Applications/company/employment-records/statistics`

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
    "totalRecords": 20,
    "withdrawnRecords": 4,
    "missingInterviewRecords": 3,
    "hiredRecords": 6,
    "rejectedRecords": 7
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `totalRecords` | int | Total historical employment records for the current company. |
| `withdrawnRecords` | int | Historical records in `Withdrawn` status. |
| `missingInterviewRecords` | int | Historical records in `MissingInterview` status. |
| `hiredRecords` | int | Historical records in `Hired` status. |
| `rejectedRecords` | int | Historical records in `Rejected` status. |

### Card Mapping

| Card Label | Field |
|---|---|
| `إجمالي السجلات` | `totalRecords` |
| `منسحب` | `withdrawnRecords` |
| `لم يحضر المقابلة` | `missingInterviewRecords` |
| `تم التوظيف` | `hiredRecords` |
| `المرفوض` | `rejectedRecords` |

### Business Note

This endpoint is only for historical records and should be used together with the employment records page, not the active applications page.

---

## 4. Interviews Page Statistics

This endpoint is for the `المقابلات` page cards.

**Method:** `GET`  
**URL:** `/api/Interviews/company/statistics`

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
    "totalInterviews": 18,
    "scheduledInterviews": 6,
    "confirmedInterviews": 4,
    "completedInterviews": 5,
    "todayInterviews": 2
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `totalInterviews` | int | Total active interviews for the current company. |
| `scheduledInterviews` | int | Interviews in `Scheduled` status. |
| `confirmedInterviews` | int | Interviews in `Confirmed` status. |
| `completedInterviews` | int | Interviews in `Completed` status. |
| `todayInterviews` | int | Interviews whose date falls on today. |

### Card Mapping

| Card Label | Field |
|---|---|
| `إجمالي المقابلات` | `totalInterviews` |
| `المقابلات المجدولة` | `scheduledInterviews` |
| `المقابلات المؤكدة` | `confirmedInterviews` |
| `المقابلات المكتملة` | `completedInterviews` |
| `مقابلات اليوم` | `todayInterviews` |

---

## 5. Company Dashboard Statistics

This endpoint is for the company `لوحة التحكم` page cards.

**Method:** `GET`  
**URL:** `/api/Company/dashboard-statistics`

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
    "activeJobsNow": 7,
    "newApplicantsThisWeek": 6,
    "interviewsToday": 2,
    "successfulHires": 5
  },
  "message": null
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `activeJobsNow` | int | Current company jobs that are published and not expired. |
| `newApplicantsThisWeek` | int | Applications created during the current week for this company’s jobs. |
| `interviewsToday` | int | Interviews scheduled for today for this company’s jobs. |
| `successfulHires` | int | Historical company applications in `Hired` status. |

### Card Mapping

| Card Label | Field |
|---|---|
| `الوظائف النشطة حالياً` | `activeJobsNow` |
| `المتقدمون الجدد هذا الأسبوع` | `newApplicantsThisWeek` |
| `مقابلات اليوم` | `interviewsToday` |
| `التوظيفات الناجحة` | `successfulHires` |

---

## Frontend Integration Notes

- Each page should call only its own statistics endpoint.
- Do not reuse one page’s statistics endpoint for another page.
- These endpoints are meant for top cards only.
- Table data, filters, and page actions should continue to use their existing dedicated endpoints.
- No request body is needed for any statistics endpoint in this document.
- No query string is needed for any statistics endpoint in this document.

---

## Suggested Requests

### Jobs page

```http
GET /api/Jobs/jobs/statistics
```

### Applications page

```http
GET /api/Applications/company/statistics
```

### Employment records page

```http
GET /api/Applications/company/employment-records/statistics
```

### Interviews page

```http
GET /api/Interviews/company/statistics
```

### Company dashboard page

```http
GET /api/Company/dashboard-statistics
```

---

## Notes For Another Model

If another model is integrating these pages, it should assume:

- every company page has its own dedicated statistics endpoint
- the statistics endpoints are separate from the paginated list endpoints
- the employment records page is historical and must not use the active applications statistics endpoint
- the company dashboard endpoint is a summary endpoint and should not be treated as a replacement for page-specific statistics
- the values returned by these endpoints are already aggregated and ready to render directly in the cards
