using System;
using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g. Guest, Student, Instructor, Admin
        
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
