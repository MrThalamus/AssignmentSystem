using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AssignmentSystem.Tests.TestSupport;

namespace AssignmentSystem.Tests.Api;

/// <summary>
/// The submission endpoints over real HTTP.
///
/// Everything below the controller is covered by the service tests; what those cannot
/// reach is the multipart binding, the file response, and the size limit Kestrel
/// enforces before any of our code runs. A PDF that uploads correctly but comes back
/// as something a reader cannot open would pass every other test in the suite.
/// </summary>
public class SubmissionUploadEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SubmissionUploadEndpointTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateConfiguredClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = ApiFactory.Password });

        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static MultipartFormDataContent Upload(byte[] pdf, string fileName, Guid? assignmentId = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        // The part name has to match the action's parameter, which is what binds it.
        form.Add(file, "file", fileName);

        if (assignmentId is { } id)
            form.Add(new StringContent(id.ToString()), "assignmentId");

        return form;
    }

    [Fact]
    public async Task A_student_can_upload_a_pdf_and_read_the_same_bytes_back()
    {
        var client = await SignInAsync(ApiFactory.StudentEmail);
        var pdf = TestPdf.Bytes("my-worksheet");

        var created = await client.PostAsync(
            "/api/submissions", Upload(pdf, "my-worksheet.pdf", _factory.AssignmentId));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var submission = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("my-worksheet.pdf", submission.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", submission.GetProperty("contentType").GetString());
        Assert.Equal(pdf.Length, submission.GetProperty("fileSizeBytes").GetInt32());

        var id = submission.GetProperty("id").GetString();
        var download = await client.GetAsync($"/api/submissions/{id}/file");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);

        // The round trip has to be byte-exact: a PDF that loses or gains a byte in
        // transit is a PDF that no longer opens.
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Replacing_a_submission_over_put_stores_the_new_pdf()
    {
        var client = await SignInAsync(ApiFactory.StudentEmail);

        var created = await client.PostAsync(
            "/api/submissions",
            Upload(TestPdf.Bytes("draft"), "draft.pdf", _factory.AssignmentId));

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // The route the UI takes once a submission exists, and the only one where the
        // file arrives without an assignment id beside it.
        var replacement = TestPdf.Bytes("final");
        var updated = await client.PutAsync($"/api/submissions/{id}", Upload(replacement, "final.pdf"));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var submission = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("final.pdf", submission.GetProperty("fileName").GetString());
        Assert.Equal(2, submission.GetProperty("attemptCount").GetInt32());

        var download = await client.GetAsync($"/api/submissions/{id}/file");
        Assert.Equal(replacement, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Uploading_something_that_is_not_a_pdf_is_rejected_as_a_field_error()
    {
        var client = await SignInAsync(ApiFactory.StudentEmail);

        var response = await client.PostAsync(
            "/api/submissions",
            Upload("PK not a pdf at all"u8.ToArray(), "sneaky.pdf", _factory.AssignmentId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Reported against the field so the upload control can show the message.
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            "not a valid PDF",
            problem.GetProperty("errors").GetProperty("file")[0].GetString());
    }

    [Fact]
    public async Task Uploading_without_a_file_is_rejected()
    {
        var client = await SignInAsync(ApiFactory.StudentEmail);

        var form = new MultipartFormDataContent
        {
            { new StringContent(_factory.AssignmentId.ToString()), "assignmentId" }
        };

        var response = await client.PostAsync("/api/submissions", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_teacher_cannot_upload_a_submission()
    {
        var client = await SignInAsync(ApiFactory.TeacherEmail);

        var response = await client.PostAsync(
            "/api/submissions",
            Upload(TestPdf.Bytes("teacher"), "teacher.pdf", _factory.AssignmentId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_download_a_submission_file()
    {
        var client = await SignInAsync(ApiFactory.StudentEmail);

        var created = await client.PostAsync(
            "/api/submissions",
            Upload(TestPdf.Bytes("private"), "private.pdf", _factory.AssignmentId));

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/submissions/{id}/file");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Uploading_to_a_closed_assignment_is_refused()
    {
        var client = await SignInAsync(ApiFactory.StudentEmail);

        var response = await client.PostAsync(
            "/api/submissions",
            Upload(TestPdf.Bytes("late"), "late.pdf", _factory.ClosedAssignmentId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
