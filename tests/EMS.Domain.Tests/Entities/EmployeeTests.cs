using EMS.Domain.Entities;
using FluentAssertions;

namespace EMS.Domain.Tests.Entities;

public class EmployeeTests
{
    [Fact]
    public void FullName_combines_first_and_last_name()
    {
        var employee = new Employee { FirstName = "Amina", LastName = "Okoro" };

        employee.FullName.Should().Be("Amina Okoro");
    }
}
