using System.Reflection;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Tests.Security;

/// <summary>
/// Role restrictions declared with attributes are easy to forget on a new endpoint,
/// and the mistake is invisible until somebody exploits it. These tests read the
/// attributes back off the controllers so an unprotected action fails the build
/// rather than shipping.
/// </summary>
public class EndpointAuthorizationTests
{
    private static readonly Type[] Controllers =
    [
        typeof(AuthController),
        typeof(UsersController),
        typeof(SubjectsController),
        typeof(CoursesController),
        typeof(AssignmentsController),
        typeof(SubmissionsController)
    ];

    /// <summary>The only endpoints that may be reached without a token.</summary>
    private static readonly HashSet<string> AnonymousByDesign =
    [
        $"{nameof(AuthController)}.{nameof(AuthController.Login)}"
    ];

    public static TheoryData<string, string> AllActions()
    {
        var data = new TheoryData<string, string>();

        foreach (var controller in Controllers)
            foreach (var action in PublicActions(controller))
                data.Add(controller.Name, action.Name);

        return data;
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public void Every_endpoint_requires_authentication_unless_it_is_login(
        string controllerName, string actionName)
    {
        var controller = Controllers.Single(c => c.Name == controllerName);
        var action = PublicActions(controller).Single(m => m.Name == actionName);

        var isAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

        if (AnonymousByDesign.Contains($"{controllerName}.{actionName}"))
        {
            Assert.True(isAnonymous, $"{controllerName}.{actionName} is expected to allow anonymous access.");
            return;
        }

        Assert.False(isAnonymous, $"{controllerName}.{actionName} must not allow anonymous access.");

        var authorized = action.GetCustomAttribute<AuthorizeAttribute>() is not null
                         || controller.GetCustomAttribute<AuthorizeAttribute>() is not null;

        Assert.True(authorized, $"{controllerName}.{actionName} is missing an [Authorize] attribute.");
    }

    [Theory]
    [InlineData(nameof(UsersController.List))]
    [InlineData(nameof(UsersController.Get))]
    [InlineData(nameof(UsersController.Create))]
    [InlineData(nameof(UsersController.Update))]
    [InlineData(nameof(UsersController.ResetPassword))]
    [InlineData(nameof(UsersController.Deactivate))]
    public void Account_management_is_administrator_only(string actionName)
    {
        AssertRoles(typeof(UsersController), actionName, Roles.Admin);
    }

    [Theory]
    [InlineData(nameof(SubjectsController.Create), Roles.Admin)]
    [InlineData(nameof(SubjectsController.Update), Roles.Admin)]
    [InlineData(nameof(SubjectsController.Delete), Roles.Admin)]
    [InlineData(nameof(SubjectsController.List), Roles.AdminOrTeacher)]
    public void Changing_the_subject_catalogue_is_administrator_only(string actionName, string expectedRoles)
    {
        AssertRoles(typeof(SubjectsController), actionName, expectedRoles);
    }

    [Theory]
    [InlineData(nameof(CoursesController.Create), Roles.Admin)]
    [InlineData(nameof(CoursesController.Update), Roles.Admin)]
    [InlineData(nameof(CoursesController.Delete), Roles.Admin)]
    [InlineData(nameof(CoursesController.AddSubject), Roles.Admin)]
    [InlineData(nameof(CoursesController.AssignTeacher), Roles.Admin)]
    [InlineData(nameof(CoursesController.RemoveSubject), Roles.Admin)]
    [InlineData(nameof(CoursesController.EnrollStudents), Roles.Admin)]
    [InlineData(nameof(CoursesController.RemoveStudent), Roles.Admin)]
    [InlineData(nameof(CoursesController.List), Roles.AdminOrTeacher)]
    [InlineData(nameof(CoursesController.ListTeachable), Roles.AdminOrTeacher)]
    public void Course_structure_is_administrator_only(string actionName, string expectedRoles)
    {
        AssertRoles(typeof(CoursesController), actionName, expectedRoles);
    }

    [Theory]
    [InlineData(nameof(AssignmentsController.Create))]
    [InlineData(nameof(AssignmentsController.Update))]
    [InlineData(nameof(AssignmentsController.Delete))]
    [InlineData(nameof(AssignmentsController.Publish))]
    [InlineData(nameof(AssignmentsController.Unpublish))]
    [InlineData(nameof(AssignmentsController.Close))]
    [InlineData(nameof(AssignmentsController.ListSubmissions))]
    public void Students_cannot_reach_assignment_management(string actionName)
    {
        AssertRoles(typeof(AssignmentsController), actionName, Roles.AdminOrTeacher);
    }

    [Theory]
    [InlineData(nameof(SubmissionsController.Submit), Roles.Student)]
    [InlineData(nameof(SubmissionsController.Update), Roles.Student)]
    [InlineData(nameof(SubmissionsController.Grade), Roles.AdminOrTeacher)]
    [InlineData(nameof(SubmissionsController.ChangeStatus), Roles.AdminOrTeacher)]
    public void Submitting_and_grading_are_separated_by_role(string actionName, string expectedRoles)
    {
        AssertRoles(typeof(SubmissionsController), actionName, expectedRoles);
    }

    [Fact]
    public void Reading_assignments_and_submissions_stays_open_to_all_three_roles()
    {
        // Both are scoped inside the service instead: a blanket role filter here
        // would stop students reading their own work.
        foreach (var (controller, action) in new[]
                 {
                     (typeof(AssignmentsController), nameof(AssignmentsController.List)),
                     (typeof(AssignmentsController), nameof(AssignmentsController.Get)),
                     (typeof(SubmissionsController), nameof(SubmissionsController.List)),
                     (typeof(SubmissionsController), nameof(SubmissionsController.Get))
                 })
        {
            var method = PublicActions(controller).Single(m => m.Name == action);
            var roles = method.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

            Assert.True(string.IsNullOrEmpty(roles),
                $"{controller.Name}.{action} should not restrict roles; scoping happens in the service.");
        }
    }

    private static void AssertRoles(Type controller, string actionName, string expectedRoles)
    {
        var action = PublicActions(controller).Single(m => m.Name == actionName);

        // An action-level attribute wins; otherwise the restriction may be declared
        // once on the controller, as it is for the administrator-only ones.
        var roles = action.GetCustomAttribute<AuthorizeAttribute>()?.Roles
                    ?? controller.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

        Assert.Equal(expectedRoles, roles);
    }

    private static IEnumerable<MethodInfo> PublicActions(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);
}
