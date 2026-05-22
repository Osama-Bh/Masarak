# Company Employment Records API Documentation

## Overview

This endpoint returns company applications that should appear in the `سجلات التوظيف` page.

These applications are historical company-side records and are no longer considered active applications in the company applications page.

## Endpoint

**Method:** `GET`  
**URL:** `/api/Applications/company/employment-records`

## Authorization

This endpoint is intended for authenticated company users.

## Query Parameters

All parameters are optional unless stated otherwise.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `Page` | int | No | Page number. Default is `1`. |
| `PageSize` | int | No | Number of items per page. Default is `10`. |
| `Search` | string | No | Filters by candidate full name or job title. |
| `ApplicationStatusId` | int | No | Filters by application status. |
| `JobId` | int | No | Filters by job. |

## Business Rules

An application is returned by this endpoint only if it belongs to the company and matches one of the following rules:

### 1. Withdrawn

Return the application when:

- `ApplicationStatus = Withdrawn`
- `ApplicationDate` is before today

Example:

- Application date: `2026-08-20`
- Current date: `2026-08-21`
- Result: the application appears in `سجلات التوظيف`

### 2. MissingInterview

Return the application when:

- `ApplicationStatus = MissingInterview`
- the latest interview date is before today

### 3. Rejected

Return the application when:

- `ApplicationStatus = Rejected`
- and one of the following is true:
  - the application has at least one interview and the latest interview date is before today
  - the application has no interview and `ApplicationDate` is before today

Important:

- cancelled interviews are represented through the application becoming `Rejected`
- there is no separate company application status called `Cancelled`

### 4. Hired

Return the application when:

- `ApplicationStatus = Hired`
- and one of the following is true:
  - the application has at least one interview and the latest interview date is before today
  - the application has no interview and `ApplicationDate` is before today

## Active Applications Behavior

Applications returned by this endpoint are removed from the company active applications endpoint:

- `GET /api/Applications/company`

That active endpoint now returns only active company applications.

## Interview Page Behavior

If an application is no longer active and becomes part of employment records, its interview should no longer appear in the company interviews page.

Affected interview page endpoint:

- `GET /api/Interviews/company`

## Response Shape

The endpoint returns a paginated result.

```json
{
  "statusCode": 200,
  "data": {
    "items": [
      {
        "applicationId": 123,
        "profilePhoto": "https://...",
        "fullName": "Candidate Name",
        "email": "candidate@example.com",
        "jobTitle": "Backend Developer",
        "applicationDate": "2026-08-20T00:00:00Z",
        "matchingPercentage": 87,
        "applicationStatus": "Rejected",
        "cvDownloadUrl": "https://...",
        "canReject": false,
        "canSchedule": false,
        "canHire": false
      }
    ],
    "currentPage": 1,
    "pageSize": 10,
    "totalCount": 1
  },
  "message": null
}
```

## Response Fields

| Field | Type | Description |
|---|---|---|
| `applicationId` | int | Application ID. |
| `profilePhoto` | string \| null | Candidate profile photo URL. |
| `fullName` | string | Candidate full name. |
| `email` | string | Candidate email. |
| `jobTitle` | string | Job title. |
| `applicationDate` | datetime | Original application date. |
| `matchingPercentage` | int \| null | Matching percentage for the application. |
| `applicationStatus` | string | Current application status. |
| `cvDownloadUrl` | string \| null | Candidate CV download URL. |
| `canReject` | bool | Action flag from backend. Usually `false` for historical records. |
| `canSchedule` | bool | Action flag from backend. Usually `false` for historical records. |
| `canHire` | bool | Action flag from backend. Usually `false` for historical records. |

## Frontend Integration Notes

- Use this endpoint specifically for the `سجلات التوظيف` page.
- Do not use `GET /api/Applications/company` for this page.
- Pagination is server-side.
- Search is server-side.
- Status filtering is server-side.
- Job filtering is server-side.
- `applicationStatus` is returned as a display string.
- `applicationDate` is the application date, not the interview date.

## Suggested Frontend Query Example

```http
GET /api/Applications/company/employment-records?Page=1&PageSize=10&Search=Ahmed&ApplicationStatusId=3&JobId=5
```

## Notes For Another Model

If another model is integrating this page, it should assume:

- this endpoint is for historical company records only
- the active applications page and the employment records page must use different endpoints
- rejected applications may represent previously cancelled interviews
- once an application is historical, its interview should not continue to appear on the company interview page
