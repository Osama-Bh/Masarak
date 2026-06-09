# Get Job Applicants Endpoint

This document provides the integration details for the "Get Job Applicants" endpoint. It is designed to retrieve a paginated, filterable, and searchable list of applicants for a specific job posting.

## Endpoint Details

- **HTTP Method:** `GET`
- **Route:** `/api/Jobs/{jobId}/applicants`
- **Authentication Required:** Yes (Bearer Token)
- **Allowed Roles:** `Company`, `Admin`

---

## Request Parameters

### Path Parameters
| Parameter | Type | Required | Description |
| :--- | :--- | :--- | :--- |
| `jobId` | `integer` | **Yes** | The unique identifier of the job you want to retrieve applicants for. |

### Query Parameters (`CompanyApplicationsRequestDTO`)
| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Page` | `integer` | `1` | The page number for pagination. |
| `PageSize` | `integer` | `10` | The number of applicants to return per page. |
| `Search` | `string` | `null` | A search term to filter applicants by their full name or job title. |
| `ApplicationStatusId` | `integer` | `null` | Filters the applicants by a specific application status ID (e.g., Pending, Shortlisted, Interviewed). |

---

## Response Structure

### Success Response (HTTP 200 OK)

The response follows the standard wrapper `ApiResponse<PaginatedResult<JobApplicantDTO>>`.

```json
{
  "statusCode": 200,
  "data": {
    "items": [
      {
        "applicationId": 142,
        "fullName": "John Doe Smith",
        "email": "john.doe@example.com",
        "matchingPercentage": 85,
        "applicationDate": "2026-06-09T10:30:00Z",
        "applicationStatus": "PendingReview",
        "cvDownloadUrl": "https://url-to-blob-storage.com/resume.pdf"
      }
    ],
    "currentPage": 1,
    "pageSize": 10,
    "totalCount": 25
  },
  "message": null,
  "errors": null
}
```

### Property Descriptions (`JobApplicantDTO`)

- `applicationId`: The unique ID of the application record.
- `fullName`: The candidate's full name.
- `email`: The candidate's email address.
- `matchingPercentage`: The AI-generated match score between the candidate's CV and the job description (0-100).
- `applicationDate`: The ISO-8601 UTC timestamp of when they applied.
- `applicationStatus`: A string representing the current status of the application (e.g., "PendingReview", "Shortlisted", "Interviewed", "Hired", "Rejected").
- `cvDownloadUrl`: The secure, pre-signed URL to download the candidate's CV/Resume. Can be `null`.

---

## Error Responses

- **401 Unauthorized:** 
  - If the user is not authenticated.
  - If the Company profile cannot be found from the authenticated user's claims.
- **403 Forbidden:** If the authenticated user does not have the `Company` or `Admin` role.
- **500 Internal Server Error:** If there is a server-side exception or database issue.

---

## Example Usage

### JavaScript / Fetch API
```javascript
const jobId = 5;
const page = 1;
const pageSize = 10;
const token = "YOUR_JWT_BEARER_TOKEN";

fetch(`https://your-api-domain.com/api/Jobs/${jobId}/applicants?Page=${page}&PageSize=${pageSize}`, {
    method: 'GET',
    headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
    }
})
.then(response => response.json())
.then(data => {
    console.log(data);
    // data.data.items contains the list of applicants
    // data.data.totalCount contains the total number of applicants matching the filters
})
.catch(error => console.error('Error fetching applicants:', error));
```
