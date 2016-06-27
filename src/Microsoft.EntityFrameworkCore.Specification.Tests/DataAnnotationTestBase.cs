// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public abstract class DataAnnotationTestBase<TTestStore, TFixture> : IClassFixture<TFixture>, IDisposable
        where TTestStore : TestStore
        where TFixture : DataAnnotationFixtureBase<TTestStore>, new()
    {
        protected DataAnnotationContext CreateContext() => Fixture.CreateContext(TestStore);

        protected DataAnnotationTestBase(TFixture fixture)
        {
            Fixture = fixture;

            TestStore = Fixture.CreateTestStore();
        }

        protected TFixture Fixture { get; }

        protected TTestStore TestStore { get; }

        public virtual void Dispose() => TestStore.Dispose();

        public virtual ModelBuilder CreateModelBuilder()
        {
            var context = CreateContext();
            var conventionSetBuilder = context.GetService<IDatabaseProviderServices>().ConventionSetBuilder;
            var conventionSet = context.GetService<ICoreConventionSetBuilder>().CreateConventionSet();
            conventionSet = conventionSetBuilder == null
                ? conventionSet
                : conventionSetBuilder.AddConventions(conventionSet);
            return new ModelBuilder(conventionSet);
        }

        protected virtual void Validate(IModel model) => Fixture.ThrowingValidator.Validate(model);

        protected class Person
        {
            public int Id { get; set; }

            [StringLength(5)]
            public string Name { get; set; }
        }

        protected class Employee : Person
        {
        }

        [Fact]
        public virtual void Explicit_configuration_on_derived_type_overrides_annotation_on_unmapped_base_type()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder
                .Entity<Employee>()
                .Property(p => p.Name)
                .HasMaxLength(10);

            Validate(modelBuilder.Model);

            Assert.Equal(10, GetProperty<Employee>(modelBuilder, "Name").GetMaxLength());
        }

        [Fact]
        public virtual void Explicit_configuration_on_derived_type_overrides_annotation_on_mapped_base_type()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder
                .Entity<Person>();

            modelBuilder
                .Entity<Employee>()
                .Property(p => p.Name)
                .HasMaxLength(10);

            Validate(modelBuilder.Model);

            Assert.Equal(10, GetProperty<Employee>(modelBuilder, "Name").GetMaxLength());
        }

        [Fact]
        public virtual void Explicit_configuration_on_derived_type_or_base_type_is_last_one_wins()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder
                .Entity<Person>()
                .Property(p => p.Name)
                .HasMaxLength(5);

            modelBuilder
                .Entity<Employee>()
                .Property(p => p.Name)
                .HasMaxLength(10);

            Assert.Equal(10, GetProperty<Person>(modelBuilder, "Name").GetMaxLength());
            Assert.Equal(10, GetProperty<Employee>(modelBuilder, "Name").GetMaxLength());

            Validate(modelBuilder.Model);

            modelBuilder = CreateModelBuilder();

            modelBuilder
                .Entity<Employee>()
                .Property(p => p.Name)
                .HasMaxLength(10);

            modelBuilder
                .Entity<Person>()
                .Property(p => p.Name)
                .HasMaxLength(5);

            Validate(modelBuilder.Model);

            Assert.Equal(5, GetProperty<Person>(modelBuilder, "Name").GetMaxLength());
            Assert.Equal(5, GetProperty<Employee>(modelBuilder, "Name").GetMaxLength());
        }

        protected static IMutableProperty GetProperty<TEntity>(ModelBuilder modelBuilder, string name)
            => modelBuilder.Model.FindEntityType(typeof(TEntity)).FindProperty(name);

        [Fact]
        public virtual void Duplicate_column_order_is_ignored()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Entity_10558>();

            Validate(modelBuilder.Model);
        }

        protected class Entity_10558
        {
            [Key]
            [Column(Order = 1)]
            public int Key1 { get; set; }

            [Key]
            [Column(Order = 1)]
            public int Key2 { get; set; }

            public string Name { get; set; }
        }

        [Fact]
        public virtual ModelBuilder Non_public_annotations_are_enabled()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<PrivateMemberAnnotationClass>().Property(
                PrivateMemberAnnotationClass.PersonFirstNameExpr);

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<PrivateMemberAnnotationClass>(modelBuilder, "PersonFirstName").IsPrimaryKey());

            return modelBuilder;
        }

        protected class PrivateMemberAnnotationClass
        {
            public static readonly Expression<Func<PrivateMemberAnnotationClass, string>> PersonFirstNameExpr =
                p => p.PersonFirstName;

            public static Expression<Func<PrivateMemberAnnotationClass, object>> PersonFirstNameObjectExpr =
                p => p.PersonFirstName;

            [Key]
            [Column("dsdsd", Order = 1, TypeName = "nvarchar(128)")]
            private string PersonFirstName { get; set; }
        }

        [Fact]
        public virtual void NotMapped_should_propagate_down_inheritance_hierarchy()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<NotMappedDerived>();

            Validate(modelBuilder.Model);

            Assert.NotNull(modelBuilder.Model.FindEntityType(typeof(NotMappedDerived)));
        }

        [NotMapped]
        protected class NotMappedBase
        {
            public int Id { get; set; }
        }

        protected class NotMappedDerived : NotMappedBase
        {
        }

        [Fact]
        public virtual void NotMapped_on_base_class_property_ignores_it()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Unit1>();
            modelBuilder.Entity<BaseEntity1>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity1)).FindProperty("BaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity1)).FindProperty("BaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit1)).FindProperty("BaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity1)).FindProperty("VirtualBaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity1)).FindProperty("VirtualBaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit1)).FindProperty("VirtualBaseClassProperty"));
        }

        protected abstract class AbstractBaseEntity1
        {
            public long Id { get; set; }
            public abstract string AbstractBaseClassProperty { get; set; }
        }

        protected class BaseEntity1 : AbstractBaseEntity1
        {
            [NotMapped]
            public string BaseClassProperty { get; set; }

            [NotMapped]
            public virtual string VirtualBaseClassProperty { get; set; }

            public override string AbstractBaseClassProperty { get; set; }
        }

        protected class Unit1 : BaseEntity1
        {
            public override string VirtualBaseClassProperty { get; set; }
            public virtual AbstractBaseEntity1 Related { get; set; }
        }

        protected class DifferentUnit1 : BaseEntity1
        {
            public new string VirtualBaseClassProperty { get; set; }
        }

        [Fact]
        public virtual void NotMapped_on_base_class_property_and_overriden_property_ignores_them()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Unit2>();
            modelBuilder.Entity<BaseEntity2>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity2)).FindProperty("VirtualBaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity2)).FindProperty("VirtualBaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit2)).FindProperty("VirtualBaseClassProperty"));
        }

        protected abstract class AbstractBaseEntity2
        {
            public long Id { get; set; }
            public abstract string AbstractBaseClassProperty { get; set; }
        }

        protected class BaseEntity2 : AbstractBaseEntity2
        {
            public string BaseClassProperty { get; set; }

            [NotMapped]
            public virtual string VirtualBaseClassProperty { get; set; }

            public override string AbstractBaseClassProperty { get; set; }
        }

        protected class Unit2 : BaseEntity2
        {
            [NotMapped]
            public override string VirtualBaseClassProperty { get; set; }

            public virtual AbstractBaseEntity2 Related { get; set; }
        }

        protected class DifferentUnit2 : BaseEntity2
        {
            public new string VirtualBaseClassProperty { get; set; }
        }

        [Fact]
        public virtual void NotMapped_on_base_class_property_discovered_through_navigation_ignores_it()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Unit3>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity3)).FindProperty("AbstractBaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity3)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit3)).FindProperty("AbstractBaseClassProperty"));
        }

        protected abstract class AbstractBaseEntity3
        {
            public long Id { get; set; }

            [NotMapped]
            public abstract string AbstractBaseClassProperty { get; set; }
        }

        protected class BaseEntity3 : AbstractBaseEntity3
        {
            public string BaseClassProperty { get; set; }
            public virtual string VirtualBaseClassProperty { get; set; }
            public override string AbstractBaseClassProperty { get; set; }
        }

        protected class Unit3 : BaseEntity3
        {
            [NotMapped]
            public override string VirtualBaseClassProperty { get; set; }

            public virtual AbstractBaseEntity3 Related { get; set; }
        }

        protected class DifferentUnit3 : BaseEntity3
        {
            public new string VirtualBaseClassProperty { get; set; }
        }

        [Fact]
        public virtual void NotMapped_on_abstract_base_class_property_ignores_it()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<AbstractBaseEntity3>();
            modelBuilder.Entity<BaseEntity3>();
            modelBuilder.Entity<Unit3>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity3)).FindProperty("AbstractBaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity3)).FindProperty("AbstractBaseClassProperty"));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit3)).FindProperty("AbstractBaseClassProperty"));
        }

        [Fact]
        public virtual void NotMapped_on_overriden_mapped_base_class_property_throws()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Ignore<DifferentUnit4>();
            modelBuilder.Entity<Unit4>();
            modelBuilder.Entity<BaseEntity4>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity4)).FindProperty("VirtualBaseClassProperty"));
            Assert.NotNull(modelBuilder.Model.FindEntityType(typeof(BaseEntity4)).FindProperty("VirtualBaseClassProperty"));
            Assert.NotNull(modelBuilder.Model.FindEntityType(typeof(Unit4)).FindProperty("VirtualBaseClassProperty"));
        }

        protected abstract class AbstractBaseEntity4
        {
            public long Id { get; set; }
            public abstract string AbstractBaseClassProperty { get; set; }
        }

        protected class BaseEntity4 : AbstractBaseEntity4
        {
            public string BaseClassProperty { get; set; }
            public virtual string VirtualBaseClassProperty { get; set; }
            public override string AbstractBaseClassProperty { get; set; }
        }

        protected class Unit4 : BaseEntity4
        {
            [NotMapped]
            public override string VirtualBaseClassProperty { get; set; }

            public virtual AbstractBaseEntity4 Related { get; set; }
        }

        protected class DifferentUnit4 : BaseEntity4
        {
            public new string VirtualBaseClassProperty { get; set; }
        }

        [Fact]
        public virtual void NotMapped_on_unmapped_derived_property_ignores_it()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Ignore<AbstractBaseEntity4>();
            modelBuilder.Ignore<BaseEntity4>();
            modelBuilder.Entity<Unit4>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity4)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity4)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit4)).FindProperty("VirtualBaseClassProperty"));
        }

        [Fact]
        public virtual void NotMapped_on_unmapped_base_class_property_and_overriden_property_ignores_it()
        {
            var modelBuilder = CreateModelBuilder();
            modelBuilder.Ignore<AbstractBaseEntity2>();
            modelBuilder.Ignore<BaseEntity2>();
            modelBuilder.Entity<Unit2>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity2)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity2)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit2)).FindProperty("VirtualBaseClassProperty"));
        }

        [Fact]
        public virtual void NotMapped_on_unmapped_base_class_property_ignores_it()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Ignore<AbstractBaseEntity1>();
            modelBuilder.Ignore<BaseEntity1>();
            modelBuilder.Entity<Unit1>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity1)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity1)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit1)).FindProperty("VirtualBaseClassProperty"));
        }

        [Fact]
        public virtual void NotMapped_on_new_property_with_same_name_as_in_unmapped_base_class_ignores_it()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<DifferentUnit5>();

            Validate(modelBuilder.Model);

            Assert.Null(modelBuilder.Model.FindEntityType(typeof(AbstractBaseEntity5)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(BaseEntity5)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(Unit5)));
            Assert.Null(modelBuilder.Model.FindEntityType(typeof(DifferentUnit5)).FindProperty("VirtualBaseClassProperty"));
        }

        protected abstract class AbstractBaseEntity5
        {
            public long Id { get; set; }
            public abstract string AbstractBaseClassProperty { get; set; }
        }

        protected class BaseEntity5 : AbstractBaseEntity5
        {
            public string BaseClassProperty { get; set; }
            public virtual string VirtualBaseClassProperty { get; set; }
            public override string AbstractBaseClassProperty { get; set; }
        }

        protected class Unit5 : BaseEntity5
        {
            public override string VirtualBaseClassProperty { get; set; }
            public virtual AbstractBaseEntity5 Related { get; set; }
        }

        protected class DifferentUnit5 : BaseEntity5
        {
            [NotMapped]
            public new string VirtualBaseClassProperty { get; set; }
        }

        [Fact]
        public virtual void StringLength_with_value_takes_presedence_over_MaxLength()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<MaxLengthAnnotationClass>();

            Validate(modelBuilder.Model);

            Assert.Equal(500, GetProperty<MaxLengthAnnotationClass>(modelBuilder, "PersonFirstName").GetMaxLength());
            Assert.Equal(500, GetProperty<MaxLengthAnnotationClass>(modelBuilder, "PersonLastName").GetMaxLength());
        }

        protected class MaxLengthAnnotationClass
        {
            public int Id { get; set; }

            [StringLength(500)]
            [MaxLength]
            public string PersonFirstName { get; set; }

            [MaxLength]
            [StringLength(500)]
            public string PersonLastName { get; set; }
        }

        [Fact]
        public virtual void MaxLength_with_length_takes_precedence_over_StringLength()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<MaxLengthWithLengthAnnotationClass>();

            Validate(modelBuilder.Model);

            Assert.Equal(500, GetProperty<MaxLengthWithLengthAnnotationClass>(modelBuilder, "PersonFirstName").GetMaxLength());
            Assert.Equal(500, GetProperty<MaxLengthWithLengthAnnotationClass>(modelBuilder, "PersonLastName").GetMaxLength());
        }

        protected class MaxLengthWithLengthAnnotationClass
        {
            public int Id { get; set; }

            [StringLength(500)]
            [MaxLength(30)]
            public string PersonFirstName { get; set; }

            [MaxLength(30)]
            [StringLength(500)]
            public string PersonLastName { get; set; }
        }

        [Fact]
        public virtual ModelBuilder Default_length_for_key_string_column()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login1>();
            modelBuilder.Ignore<Profile1>();

            Validate(modelBuilder.Model);

            return modelBuilder;
        }

        protected class Login1
        {
            public int Login1Id { get; set; }

            [Key]
            public string UserName { get; set; }

            public virtual Profile1 Profile { get; set; }
        }

        protected class Profile1
        {
            public int Profile1Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public virtual Login1 User { get; set; }
        }

        [Fact]
        public virtual ModelBuilder Key_and_column_work_together()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<ColumnKeyAnnotationClass1>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<ColumnKeyAnnotationClass1>(modelBuilder, "PersonFirstName").IsPrimaryKey());

            return modelBuilder;
        }

        protected class ColumnKeyAnnotationClass1
        {
            [Key]
            [Column("dsdsd", Order = 1, TypeName = "nvarchar(128)")]
            public string PersonFirstName { get; set; }
        }

        [Fact]
        public virtual ModelBuilder Key_and_MaxLength_64_produce_nvarchar_64()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<ColumnKeyAnnotationClass2>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<ColumnKeyAnnotationClass2>(modelBuilder, "PersonFirstName").IsPrimaryKey());
            Assert.Equal(64, GetProperty<ColumnKeyAnnotationClass2>(modelBuilder, "PersonFirstName").GetMaxLength());

            return modelBuilder;
        }

        protected class ColumnKeyAnnotationClass2
        {
            [Key]
            [MaxLength(64)]
            public string PersonFirstName { get; set; }
        }

        [Fact]
        public virtual void Key_from_base_type_is_recognized()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<SRelated>();
            modelBuilder.Entity<OKeyBase>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<OKeyBase>(modelBuilder, "OrderLineNo").IsPrimaryKey());
            Assert.True(GetProperty<DODerived>(modelBuilder, "OrderLineNo").IsPrimaryKey());
        }

        [Fact]
        public virtual void Key_from_base_type_is_recognized_if_base_discovered_first()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<OKeyBase>();
            modelBuilder.Entity<SRelated>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<OKeyBase>(modelBuilder, "OrderLineNo").IsPrimaryKey());
            Assert.True(GetProperty<DODerived>(modelBuilder, "OrderLineNo").IsPrimaryKey());
        }

        protected class SRelated
        {
            public int SRelatedId { get; set; }
            public ICollection<DODerived> DADeriveds { get; set; }
        }

        protected class OKeyBase
        {
            [Key]
            public int OrderLineNo { get; set; }

            public int Quantity { get; set; }
        }

        protected class DODerived : OKeyBase
        {
            public SRelated DARelated { get; set; }
            public string Special { get; set; }
        }

        [Fact]
        public virtual void Key_on_nav_prop_is_ignored()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<KeyOnNavProp>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<KeyOnNavProp>(modelBuilder, "Id").IsPrimaryKey());
        }

        protected class DASimple
        {
            public int Id { get; set; }
        }

        protected class KeyOnNavProp
        {
            public int Id { get; set; }

            [Key]
            public ICollection<DASimple> Simples { get; set; }

            [Key]
            public DASimple SpecialSimple { get; set; }
        }

        [Fact]
        public virtual ModelBuilder Timestamp_takes_precedence_over_MaxLength()
        {
            var modelBuilder = CreateModelBuilder();
            modelBuilder.Entity<TimestampAndMaxlen>().Ignore(x => x.NonMaxTimestamp);

            Validate(modelBuilder.Model);

            Assert.Null(GetProperty<TimestampAndMaxlen>(modelBuilder, "MaxTimestamp").GetMaxLength());

            return modelBuilder;
        }

        [Fact]
        public virtual ModelBuilder Timestamp_takes_precedence_over_MaxLength_with_value()
        {
            var modelBuilder = CreateModelBuilder();
            modelBuilder.Entity<TimestampAndMaxlen>().Ignore(x => x.MaxTimestamp);

            Validate(modelBuilder.Model);

            Assert.Equal(100, GetProperty<TimestampAndMaxlen>(modelBuilder, "NonMaxTimestamp").GetMaxLength());

            return modelBuilder;
        }

        protected class TimestampAndMaxlen
        {
            public int Id { get; set; }

            [MaxLength]
            [Timestamp]
            public byte[] MaxTimestamp { get; set; }

            [MaxLength(100)]
            [Timestamp]
            public byte[] NonMaxTimestamp { get; set; }
        }

        [Fact]
        public virtual void Annotation_in_derived_class_when_base_class_processed_after_derived_class()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<StyledProduct>();
            modelBuilder.Entity<Product>();

            Validate(modelBuilder.Model);

            Assert.Equal(150, GetProperty<StyledProduct>(modelBuilder, "Style").GetMaxLength());
        }

        protected class Product
        {
            public virtual int ProductID { get; set; }
        }

        protected class StyledProduct : Product
        {
            [StringLength(150)]
            public virtual string Style { get; set; }
        }

        [Fact]
        public virtual void Required_and_ForeignKey_to_Required()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login2>();
            modelBuilder.Entity<Profile2>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Login2>(modelBuilder, "Login2Id").IsForeignKey());
        }

        protected class Login2
        {
            public int Login2Id { get; set; }
            public string UserName { get; set; }

            [Required]
            [ForeignKey("Login2Id")]
            public virtual Profile2 Profile { get; set; }
        }

        protected class Profile2
        {
            public int Profile2Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            [Required]
            public virtual Login2 User { get; set; }
        }

        [Fact]
        // Regression test for Dev11 Bug 94993
        public virtual void Required_to_Required_and_ForeignKey()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login3>();
            modelBuilder.Entity<Profile3>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Profile3>(modelBuilder, "Profile3Id").IsForeignKey());
        }

        protected class Login3
        {
            public int Login3Id { get; set; }
            public string UserName { get; set; }

            [Required]
            public virtual Profile3 Profile { get; set; }
        }

        protected class Profile3
        {
            public int Profile3Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            [Required]
            [ForeignKey("Profile3Id")]
            public virtual Login3 User { get; set; }
        }

        [Fact]
        public virtual void Required_and_ForeignKey_to_Required_and_ForeignKey()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login4>();
            modelBuilder.Entity<Profile4>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Profile4>(modelBuilder, "Profile4Id").IsForeignKey());
        }

        protected class Login4
        {
            public int Login4Id { get; set; }
            public string UserName { get; set; }

            [Required]
            [ForeignKey("Login4Id")]
            public virtual Profile4 Profile { get; set; }
        }

        protected class Profile4
        {
            public int Profile4Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            [Required]
            [ForeignKey("Profile4Id")]
            public virtual Login4 User { get; set; }
        }

        [Fact]
        public virtual void ForeignKey_to_nothing()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login5>();
            modelBuilder.Entity<Profile5>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Login5>(modelBuilder, "Login5Id").IsForeignKey());
        }

        protected class Login5
        {
            public int Login5Id { get; set; }
            public string UserName { get; set; }

            [ForeignKey("Login5Id")]
            public virtual Profile5 Profile { get; set; }
        }

        protected class Profile5
        {
            public int Profile5Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            public virtual Login5 User { get; set; }
        }

        [Fact]
        public virtual void Required_and_ForeignKey_to_nothing()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login6>();
            modelBuilder.Entity<Profile6>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Login6>(modelBuilder, "Login6Id").IsForeignKey());
        }

        protected class Login6
        {
            public int Login6Id { get; set; }
            public string UserName { get; set; }

            [Required]
            [ForeignKey("Login6Id")]
            public virtual Profile6 Profile { get; set; }
        }

        protected class Profile6
        {
            public int Profile6Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            public virtual Login6 User { get; set; }
        }

        [Fact]
        public virtual void Nothing_to_ForeignKey()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login7>();
            modelBuilder.Entity<Profile7>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Profile7>(modelBuilder, "Profile7Id").IsForeignKey());
        }

        protected class Login7
        {
            public int Login7Id { get; set; }
            public string UserName { get; set; }

            public virtual Profile7 Profile { get; set; }
        }

        protected class Profile7
        {
            public int Profile7Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            [ForeignKey("Profile7Id")]
            public virtual Login7 User { get; set; }
        }

        [Fact]
        public virtual void Nothing_to_Required_and_ForeignKey()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login8>();
            modelBuilder.Entity<Profile8>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Profile8>(modelBuilder, "Profile8Id").IsForeignKey());
        }

        protected class Login8
        {
            public int Login8Id { get; set; }
            public string UserName { get; set; }

            public virtual Profile8 Profile { get; set; }
        }

        protected class Profile8
        {
            public int Profile8Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            [Required]
            [ForeignKey("Profile8Id")]
            public virtual Login8 User { get; set; }
        }

        [Fact]
        public virtual void ForeignKey_to_ForeignKey()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<Login9>();
            modelBuilder.Entity<Profile9>();

            Validate(modelBuilder.Model);

            Assert.True(GetProperty<Login9>(modelBuilder, "Login9Id").IsForeignKey());
            Assert.True(GetProperty<Profile9>(modelBuilder, "Profile9Id").IsForeignKey());
        }

        protected class Login9
        {
            public int Login9Id { get; set; }
            public string UserName { get; set; }

            [ForeignKey("Login9Id")]
            public virtual Profile9 Profile { get; set; }
        }

        protected class Profile9
        {
            public int Profile9Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }

            [ForeignKey("Profile9Id")]
            public virtual Login9 User { get; set; }
        }

        [Fact]
        public virtual ModelBuilder TableNameAttribute_affects_table_name_in_TPH()
        {
            var modelBuilder = CreateModelBuilder();

            modelBuilder.Entity<TNAttrBase>();
            modelBuilder.Entity<TNAttrDerived>();

            Validate(modelBuilder.Model);

            return modelBuilder;
        }

        [Table("A")]
        protected class TNAttrBase
        {
            public int Id { get; set; }
            public string BaseData { get; set; }
        }

        protected class TNAttrDerived : TNAttrBase
        {
            public string DerivedData { get; set; }
        }

        [Fact]
        public virtual void ConcurrencyCheckAttribute_throws_if_value_in_database_changed()
        {
            using (var context = CreateContext())
            {
                var clientRow = context.Ones.First(r => r.UniqueNo == 1);
                clientRow.RowVersion = new Guid("00000000-0000-0000-0002-000000000001");
                clientRow.RequiredColumn = "ChangedData";

                using (var innerContext = CreateContext())
                {
                    var storeRow = innerContext.Ones.First(r => r.UniqueNo == 1);
                    storeRow.RowVersion = new Guid("00000000-0000-0000-0003-000000000001");
                    storeRow.RequiredColumn = "ModifiedData";

                    innerContext.SaveChanges();
                }

                Assert.Throws<DbUpdateConcurrencyException>(() => context.SaveChanges());
            }
        }

        [Fact]
        public virtual void DatabaseGeneratedAttribute_autogenerates_values_when_set_to_identity()
        {
            using (var context = CreateContext())
            {
                context.Ones.Add(new One { RequiredColumn = "Third", RowVersion = new Guid("00000000-0000-0000-0000-000000000003") });

                context.SaveChanges();
            }
        }

        [Fact]
        public virtual void MaxLengthAttribute_throws_while_inserting_value_longer_than_max_length()
        {
            using (var context = CreateContext())
            {
                context.Ones.Add(new One { RequiredColumn = "ValidString", RowVersion = new Guid("00000000-0000-0000-0000-000000000001"), MaxLengthProperty = "Short" });

                context.SaveChanges();
            }

            using (var context = CreateContext())
            {
                context.Ones.Add(new One { RequiredColumn = "ValidString", RowVersion = new Guid("00000000-0000-0000-0000-000000000002"), MaxLengthProperty = "VeryVeryVeryVeryVeryVeryLongString" });

                Assert.Equal("An error occurred while updating the entries. See the inner exception for details.",
                    Assert.Throws<DbUpdateException>(() => context.SaveChanges()).Message);
            }
        }

        [Fact]
        public virtual void NotMappedAttribute_ignores_entityType()
        {
            using (var context = CreateContext())
            {
                Assert.False(context.Model.GetEntityTypes().Any(e => e.Name == typeof(C).FullName));
            }
        }

        [Fact]
        public virtual void NotMappedAttribute_ignores_navigation()
        {
            using (var context = CreateContext())
            {
                Assert.False(context.Model.GetEntityTypes().Any(e => e.Name == typeof(UselessBookDetails).FullName));
            }
        }

        [Fact]
        public virtual void NotMappedAttribute_ignores_property()
        {
            using (var context = CreateContext())
            {
                Assert.Null(context.Model.GetEntityTypes().First(e => e.Name == typeof(One).FullName).FindProperty("IgnoredProperty"));
            }
        }

        [Fact]
        public virtual void RequiredAttribute_for_navigation_throws_while_inserting_null_value()
        {
            using (var context = CreateContext())
            {
                context.BookDetails.Add(new BookDetail { BookId = "Book1" });

                context.SaveChanges();
            }

            using (var context = CreateContext())
            {
                context.BookDetails.Add(new BookDetail());

                Assert.Equal("An error occurred while updating the entries. See the inner exception for details.",
                    Assert.Throws<DbUpdateException>(() => context.SaveChanges()).Message);
            }
        }

        [Fact]
        public virtual void RequiredAttribute_does_nothing_when_specified_on_nav_to_dependent_per_convention()
        {
            using (var context = CreateContext())
            {
                var relationship = context.Model.FindEntityType(typeof(AdditionalBookDetail))
                    .FindNavigation(nameof(AdditionalBookDetail.BookDetail)).ForeignKey;
                Assert.Equal(typeof(AdditionalBookDetail), relationship.PrincipalEntityType.ClrType);
                Assert.False(relationship.IsRequired);
            }
        }

        [Fact]
        public virtual void RequiredAttribute_for_property_throws_while_inserting_null_value()
        {
            using (var context = CreateContext())
            {
                context.Ones.Add(new One { RequiredColumn = "ValidString", RowVersion = new Guid("00000000-0000-0000-0000-000000000001") });

                context.SaveChanges();
            }

            using (var context = CreateContext())
            {
                context.Ones.Add(new One { RequiredColumn = null, RowVersion = new Guid("00000000-0000-0000-0000-000000000002") });

                Assert.Equal("An error occurred while updating the entries. See the inner exception for details.",
                    Assert.Throws<DbUpdateException>(() => context.SaveChanges()).Message);
            }
        }

        [Fact]
        public virtual void StringLengthAttribute_throws_while_inserting_value_longer_than_max_length()
        {
            using (var context = CreateContext())
            {
                context.Twos.Add(new Two { Data = "ValidString" });

                context.SaveChanges();
            }

            using (var context = CreateContext())
            {
                context.Twos.Add(new Two { Data = "ValidButLongString" });

                Assert.Equal("An error occurred while updating the entries. See the inner exception for details.",
                    Assert.Throws<DbUpdateException>(() => context.SaveChanges()).Message);
            }
        }

        [Fact]
        public virtual void TimestampAttribute_throws_if_value_in_database_changed()
        {
            using (var context = CreateContext())
            {
                var clientRow = context.Twos.First(r => r.Id == 1);
                clientRow.Data = "ChangedData";

                using (var innerContext = CreateContext())
                {
                    var storeRow = innerContext.Twos.First(r => r.Id == 1);
                    storeRow.Data = "ModifiedData";

                    innerContext.SaveChanges();
                }

                Assert.Throws<DbUpdateConcurrencyException>(() => context.SaveChanges());
            }
        }
    }
}
