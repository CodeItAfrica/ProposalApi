using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Domain
{
    public class Permission
    {
        [Key]
        public long PermissionID { get; set; }
        public string? PermissionName { get; set; }
        public string? Module { get; set; }
        public string? Category { get; set; }
        public string? Remarks { get; set; }

        public ICollection<RolePermission>? RolePermissions { get; set; }
    }

    [Table("PasswordResets")]
    public class PasswordReset
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Email { get; set; }

        [Required]
        [MaxLength(100)]
        public string ResetToken { get; set; }

        [Required]
        public DateTime TokenExpiry { get; set; }

        [Required]
        [MaxLength(20)]
        public string UserType { get; set; } // "User", "Agent", or "Customer"

        [MaxLength(50)]
        public string UserId { get; set; } // Stores UserID, AgentID, or InsuredID

        public bool IsUsed { get; set; } // Track if token has been used

        public DateTime CreatedAt { get; set; }

        public DateTime? UsedAt { get; set; } // When the token was used

        [MaxLength(50)]
        public string IpAddress { get; set; } // Track IP for security

        [MaxLength(500)]
        public string UserAgent { get; set; } // Track browser/device
    }

    public class UserRole
    {
        public long UserID { get; set; }
        public User? User { get; set; }

        public long RoleID { get; set; }

        public Role? Role { get; set; }

        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }


    [Keyless] // Correctly apply the Keyless attribute to the class declaration
    public class RolePermission
    {
        public long RoleID { get; set; }
        public Role? Role { get; set; }

        public long PermissionID { get; set; }
        public Permission? Permission { get; set; }

        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class UserPermission
    {
        public long UserID { get; set; }
        public User? User { get; set; }

        public long PermissionID { get; set; }
        public Permission? Permission { get; set; }
    }

    public class Role
    {
        public long RoleID { get; set; }
        public string? RoleName { get; set; }
        public string? RoleDescription { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public ICollection<UserRole>? UserRoles { get; set; }
        public ICollection<RolePermission>? RolePermissions { get; set; }

    }



    public class User
    {
        public long UserID { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime? PwdExpiry { get; set; }
        [MaxLength(200)] public string? SecQuestion { get; set; }
        [MaxLength(200)] public string? SecAnswer { get; set; } // store hashed too
        public byte? Active { get; set; } = 1;                   // tinyint
        [MaxLength(100)] public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        [MaxLength(100)] public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public ICollection<UserRole>? UserRoles { get; set; }
        public ICollection<UserPermission>? UserPermissions { get; set; }

    }



    public class RegisterUserRequest
    {
        [Required, MaxLength(100)]
        public string Username { get; set; } = default!;
        [Required, MaxLength(200)]
        public string Password { get; set; } = default!;
        [MaxLength(100)] public string? FirstName { get; set; }
        [MaxLength(100)] public string? LastName { get; set; }
        [MaxLength(200)] public string? Address { get; set; }
        [MaxLength(200)] public string? Email { get; set; }
        [MaxLength(50)] public string? Phone { get; set; }
        public bool Active { get; set; } = true;
    }

    public class UpdateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool? Active { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required] public string CurrentPassword { get; set; } = default!;
        [Required] public string NewPassword { get; set; } = default!;
    }

    public class CreateRoleRequest
    {
        [Required] public string RoleName { get; set; } = default!;
        public string? RoleDescription { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string? RoleName { get; set; }
        public string? RoleDescription { get; set; }
    }

    public class CreatePermissionRequest
    {
        [Required] public string PermissionName { get; set; } = default!;
        public string? Module { get; set; }
        public string? Category { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdatePermissionRequest
    {
        public string? PermissionName { get; set; }
        public string? Module { get; set; }
        public string? Category { get; set; }
        public string? Remarks { get; set; }
    }

    public class AssignRoleToUserRequest
    {
        [Required] public long UserId { get; set; }
        [Required] public long RoleId { get; set; }
    }
    public class AddPermissionDto
    {
        public string PermissionName { get; set; }
    }

    public class RemoveRoleFromUserRequest
    {
        [Required] public long UserId { get; set; }
        [Required] public long RoleId { get; set; }
    }

    public class AssignPermissionToRoleRequest
    {
        [Required] public long RoleId { get; set; }
        [Required] public long PermissionId { get; set; }
    }

    public class RemovePermissionFromRoleRequest
    {
        [Required] public long RoleId { get; set; }
        [Required] public long PermissionId { get; set; }
    }
}
