//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AgreementSignature
{
    using System;
    using System.Collections.Generic;
    
    public partial class ClaimSet
    {
        public int Id { get; set; }
        public int Role { get; set; }
        public Nullable<int> District_Id { get; set; }
        public Nullable<int> Region_Id { get; set; }
        public string Discriminator { get; set; }
        public Nullable<int> User_Id { get; set; }
        public System.DateTime DateCreated { get; set; }
    
        public virtual User User { get; set; }
    }
}
