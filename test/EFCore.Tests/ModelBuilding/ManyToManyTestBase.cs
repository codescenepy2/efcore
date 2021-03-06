// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Xunit;

// ReSharper disable InconsistentNaming
namespace Microsoft.EntityFrameworkCore.ModelBuilding
{
    public abstract partial class ModelBuilderTest
    {
        public abstract class ManyToManyTestBase : ModelBuilderTestBase
        {
            [ConditionalFact]
            public virtual void Finds_existing_navigations_and_uses_associated_FK()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Entity<Category>().Ignore(c => c.Products);
                modelBuilder.Entity<Product>().Ignore(p => p.Categories);

                modelBuilder.Entity<Category>()
                    .HasMany(o => o.Products).WithMany(c => c.Categories)
                    .UsingEntity<ProductCategory>(
                        pcb => pcb.HasOne(pc => pc.Product).WithMany(),
                        pcb => pcb.HasOne(pc => pc.Category).WithMany(c => c.ProductCategories))
                    .HasKey(pc => new { pc.ProductId, pc.CategoryId });

                var productType = model.FindEntityType(typeof(Product));
                var categoryType = model.FindEntityType(typeof(Category));
                var productCategoryType = model.FindEntityType(typeof(ProductCategory));

                var categoriesNavigation = productType.GetSkipNavigations().Single();
                var productsNavigation = categoryType.GetSkipNavigations().Single();

                var categoriesFk = categoriesNavigation.ForeignKey;
                var productsFk = productsNavigation.ForeignKey;

                Assert.Same(categoriesFk, productCategoryType.GetForeignKeys().Last());
                Assert.Same(productsFk, productCategoryType.GetForeignKeys().First());
                Assert.Equal(2, productCategoryType.GetForeignKeys().Count());

                modelBuilder.Entity<Category>()
                    .HasMany(o => o.Products).WithMany(c => c.Categories)
                    .UsingEntity<ProductCategory>(
                        pcb => pcb.HasOne(pc => pc.Product).WithMany(),
                        pcb => pcb.HasOne(pc => pc.Category).WithMany(c => c.ProductCategories));

                modelBuilder.FinalizeModel();

                Assert.Same(categoriesNavigation, productType.GetSkipNavigations().Single());
                Assert.Same(productsNavigation, categoryType.GetSkipNavigations().Single());
                Assert.Same(categoriesFk, productCategoryType.GetForeignKeys().Last());
                Assert.Same(productsFk, productCategoryType.GetForeignKeys().First());
                Assert.Equal(2, productCategoryType.GetForeignKeys().Count());
            }

            [ConditionalFact]
            public virtual void Finds_existing_navigations_and_uses_associated_FK_with_fields()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Entity<ManyToManyPrincipalWithField>(e =>
                {
                    e.Property(p => p.Id);
                    e.Property(p => p.Name);
                    e.HasKey(p => p.Id);
                });
                modelBuilder.Entity<DependentWithField>(e =>
                {
                    e.Property(d => d.DependentWithFieldId);
                    e.Property(d => d.AnotherOneToManyPrincipalId);
                    e.Ignore(d => d.OneToManyPrincipal);
                    e.Ignore(d => d.OneToOnePrincipal);
                    e.HasKey(d => d.DependentWithFieldId);
                });

                modelBuilder.Entity<ManyToManyPrincipalWithField>()
                    .HasMany(p => p.Dependents)
                    .WithMany(d => d.ManyToManyPrincipals)
                    .UsingEntity<ManyToManyJoinWithFields>(
                        jwf => jwf.HasOne<DependentWithField>(j => j.DependentWithField)
                            .WithMany(),
                        jwf => jwf.HasOne<ManyToManyPrincipalWithField>(j => j.ManyToManyPrincipalWithField)
                            .WithMany())
                    .HasKey(j => new { j.DependentWithFieldId, j.ManyToManyPrincipalWithFieldId });

                var principalEntityType = model.FindEntityType(typeof(ManyToManyPrincipalWithField));
                var dependentEntityType = model.FindEntityType(typeof(DependentWithField));
                var joinEntityType = model.FindEntityType(typeof(ManyToManyJoinWithFields));

                var principalToJoinNav = principalEntityType.GetSkipNavigations().Single();
                var dependentToJoinNav = dependentEntityType.GetSkipNavigations().Single();

                var principalToDependentFk = principalToJoinNav.ForeignKey;
                var dependentToPrincipalFk = dependentToJoinNav.ForeignKey;

                Assert.Equal(2, joinEntityType.GetForeignKeys().Count());
                Assert.Same(principalToDependentFk, joinEntityType.GetForeignKeys().Last());
                Assert.Same(dependentToPrincipalFk, joinEntityType.GetForeignKeys().First());

                modelBuilder.Entity<ManyToManyPrincipalWithField>()
                    .HasMany(p => p.Dependents)
                    .WithMany(d => d.ManyToManyPrincipals)
                    .UsingEntity<ManyToManyJoinWithFields>(
                        jwf => jwf.HasOne<DependentWithField>(j => j.DependentWithField)
                            .WithMany(),
                        jwf => jwf.HasOne<ManyToManyPrincipalWithField>(j => j.ManyToManyPrincipalWithField)
                            .WithMany());

                modelBuilder.FinalizeModel();

                Assert.Same(principalToJoinNav, principalEntityType.GetSkipNavigations().Single());
                Assert.Same(dependentToJoinNav, dependentEntityType.GetSkipNavigations().Single());
                Assert.Equal(2, joinEntityType.GetForeignKeys().Count());
                Assert.Same(principalToDependentFk, joinEntityType.GetForeignKeys().Last());
                Assert.Same(dependentToPrincipalFk, joinEntityType.GetForeignKeys().First());
            }

            [ConditionalFact]
            public virtual void Join_type_is_automatically_configured_by_convention()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Entity<ImplicitManyToManyA>();

                var manyToManyA = model.FindEntityType(typeof(ImplicitManyToManyA));
                var manyToManyB = model.FindEntityType(typeof(ImplicitManyToManyB));
                var joinEntityType = model.GetEntityTypes()
                    .Where(et => ((EntityType)et).IsImplicitlyCreatedJoinEntityType)
                    .Single();
                Assert.Equal("ImplicitManyToManyAImplicitManyToManyB", joinEntityType.Name);

                var navigationOnManyToManyA = manyToManyA.GetSkipNavigations().Single();
                var navigationOnManyToManyB = manyToManyB.GetSkipNavigations().Single();
                Assert.Equal("Bs", navigationOnManyToManyA.Name);
                Assert.Equal("As", navigationOnManyToManyB.Name);
                Assert.Same(navigationOnManyToManyA.Inverse, navigationOnManyToManyB);
                Assert.Same(navigationOnManyToManyB.Inverse, navigationOnManyToManyA);

                var manyToManyAForeignKey = navigationOnManyToManyA.ForeignKey;
                var manyToManyBForeignKey = navigationOnManyToManyB.ForeignKey;
                Assert.NotNull(manyToManyAForeignKey);
                Assert.NotNull(manyToManyBForeignKey);
                Assert.Equal(2, joinEntityType.GetForeignKeys().Count());
                Assert.Equal(manyToManyAForeignKey.DeclaringEntityType, joinEntityType);
                Assert.Equal(manyToManyBForeignKey.DeclaringEntityType, joinEntityType);

                var key = joinEntityType.FindPrimaryKey();
                Assert.Equal(
                    new[] {
                        nameof(ImplicitManyToManyA) + "_" + nameof(ImplicitManyToManyA.Id),
                        nameof(ImplicitManyToManyB) + "_" + nameof(ImplicitManyToManyB.Id) },
                    key.Properties.Select(p => p.Name));

                modelBuilder.FinalizeModel();
            }

            [ConditionalFact]
            public virtual void Join_type_is_not_automatically_configured_when_navigations_are_ambiguous()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Entity<Hob>();

                var hob = model.FindEntityType(typeof(Hob));
                var nob = model.FindEntityType(typeof(Nob));
                Assert.NotNull(hob);
                Assert.NotNull(nob);
                Assert.Empty(model.GetEntityTypes()
                    .Where(et => ((EntityType)et).IsImplicitlyCreatedJoinEntityType));

                Assert.Empty(hob.GetSkipNavigations());
                Assert.Empty(nob.GetSkipNavigations());
            }

            [ConditionalFact]
            public virtual void Can_configure_join_type_using_fluent_api()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Entity<Category>().Ignore(c => c.Products);
                modelBuilder.Entity<Product>().Ignore(p => p.Categories);

                var manyToMany = modelBuilder.Entity<Category>()
                    .HasMany(o => o.Products).WithMany(c => c.Categories)
                    .UsingEntity<ProductCategory>(
                        pcb => pcb.HasOne(pc => pc.Product).WithMany(),
                        pcb => pcb.HasOne(pc => pc.Category).WithMany(c => c.ProductCategories),
                        pcb => pcb.HasKey(pc => new { pc.ProductId, pc.CategoryId }));

                modelBuilder.FinalizeModel();

                Assert.Equal(typeof(Category), manyToMany.Metadata.ClrType);

                var productType = model.FindEntityType(typeof(Product));
                var categoryType = model.FindEntityType(typeof(Category));
                var productCategoryType = model.FindEntityType(typeof(ProductCategory));

                var categoriesNavigation = productType.GetSkipNavigations().Single();
                var productsNavigation = categoryType.GetSkipNavigations().Single();

                var categoriesFk = categoriesNavigation.ForeignKey;
                var productsFk = productsNavigation.ForeignKey;

                Assert.Same(categoriesFk, productCategoryType.GetForeignKeys().Last());
                Assert.Same(productsFk, productCategoryType.GetForeignKeys().First());
                Assert.Equal(2, productCategoryType.GetForeignKeys().Count());

                var key = productCategoryType.FindPrimaryKey();
                Assert.Equal(
                    new[] { nameof(ProductCategory.ProductId), nameof(ProductCategory.CategoryId) },
                    key.Properties.Select(p => p.Name));
            }

            [ConditionalFact]
            public virtual void Can_ignore_existing_navigations()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;
                modelBuilder.Entity<Category>()
                    .HasMany(p => p.Products).WithMany(c => c.Categories);

                modelBuilder.Entity<Category>().Ignore(c => c.Products);
                modelBuilder.Entity<Product>().Ignore(p => p.Categories);

                // Issue #19550
                modelBuilder.Ignore<ProductCategory>();

                var productType = model.FindEntityType(typeof(Product));
                var categoryType = model.FindEntityType(typeof(Category));

                Assert.Empty(productType.GetSkipNavigations());
                Assert.Empty(categoryType.GetSkipNavigations());

                modelBuilder.FinalizeModel();
            }

            [ConditionalFact]
            public virtual void Throws_for_conflicting_many_to_one_on_left()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                // make sure we do not set up the automatic many-to-many relationship
                modelBuilder.Entity<Category>().Ignore(e => e.Products);

                modelBuilder.Entity<Category>()
                    .HasMany(o => o.Products).WithOne();

                Assert.Equal(
                    CoreStrings.ConflictingRelationshipNavigation(
                        nameof(Category) + "." + nameof(Category.Products),
                        nameof(Product) + "." + nameof(Product.Categories),
                        nameof(Category) + "." + nameof(Category.Products),
                        nameof(Product)),
                    Assert.Throws<InvalidOperationException>(
                        () => modelBuilder.Entity<Category>()
                            .HasMany(o => o.Products).WithMany(c => c.Categories)).Message);
            }

            [ConditionalFact]
            public virtual void Throws_for_conflicting_many_to_one_on_right()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                // make sure we do not set up the automatic many-to-many relationship
                modelBuilder.Entity<Category>().Ignore(e => e.Products);

                modelBuilder.Entity<Category>()
                    .HasMany(o => o.Products).WithOne();

                Assert.Equal(
                    CoreStrings.ConflictingRelationshipNavigation(
                        nameof(Product) + "." + nameof(Product.Categories),
                        nameof(Category) + "." + nameof(Category.Products),
                        nameof(Category) + "." + nameof(Category.Products),
                        nameof(Product)),
                    Assert.Throws<InvalidOperationException>(
                        () => modelBuilder.Entity<Product>()
                            .HasMany(o => o.Categories).WithMany(c => c.Products)).Message);
            }

            [ConditionalFact]
            public virtual void Throws_for_many_to_many_with_only_one_navigation_configured()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                Assert.Equal(
                    CoreStrings.MissingInverseManyToManyNavigation(
                        nameof(ManyToManyNavPrincipal),
                        nameof(NavDependent)),
                    Assert.Throws<InvalidOperationException>(
                        () => modelBuilder.Entity<ManyToManyNavPrincipal>()
                                .HasMany<NavDependent>(/* leaving empty causes the exception */)
                                .WithMany(d => d.ManyToManyPrincipals)).Message);
            }

            [ConditionalFact]
            public virtual void Navigation_properties_can_set_access_mode_using_expressions()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Entity<ManyToManyNavPrincipal>()
                    .HasMany(e => e.Dependents)
                    .WithMany(e => e.ManyToManyPrincipals);

                modelBuilder.Entity<ManyToManyNavPrincipal>()
                    .Navigation(e => e.Dependents)
                    .UsePropertyAccessMode(PropertyAccessMode.Field);

                modelBuilder.Entity<NavDependent>()
                    .Navigation(e => e.ManyToManyPrincipals)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);

                var principal = (IEntityType)model.FindEntityType(typeof(ManyToManyNavPrincipal));
                var dependent = (IEntityType)model.FindEntityType(typeof(NavDependent));

                Assert.Equal(PropertyAccessMode.Field, principal.FindSkipNavigation("Dependents").GetPropertyAccessMode());
                Assert.Equal(PropertyAccessMode.Property, dependent.FindSkipNavigation("ManyToManyPrincipals").GetPropertyAccessMode());
            }

            [ConditionalFact]
            public virtual void Navigation_properties_can_set_access_mode_using_navigation_names()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Entity<ManyToManyNavPrincipal>()
                    .HasMany<NavDependent>("Dependents")
                    .WithMany("ManyToManyPrincipals");

                modelBuilder.Entity<ManyToManyNavPrincipal>()
                    .Navigation("Dependents")
                    .UsePropertyAccessMode(PropertyAccessMode.Field);

                modelBuilder.Entity<NavDependent>()
                    .Navigation("ManyToManyPrincipals")
                    .UsePropertyAccessMode(PropertyAccessMode.Property);

                var principal = (IEntityType)model.FindEntityType(typeof(ManyToManyNavPrincipal));
                var dependent = (IEntityType)model.FindEntityType(typeof(NavDependent));

                Assert.Equal(PropertyAccessMode.Field, principal.FindSkipNavigation("Dependents").GetPropertyAccessMode());
                Assert.Equal(PropertyAccessMode.Property, dependent.FindSkipNavigation("ManyToManyPrincipals").GetPropertyAccessMode());
            }

            [ConditionalFact]
            public virtual void Can_use_shared_Type_as_join_entity()
            {
                var modelBuilder = CreateModelBuilder();
                var model = modelBuilder.Model;

                modelBuilder.Ignore<OneToManyNavPrincipal>();
                modelBuilder.Ignore<OneToOneNavPrincipal>();

                modelBuilder.Entity<ManyToManyNavPrincipal>()
                    .HasMany(e => e.Dependents)
                    .WithMany(e => e.ManyToManyPrincipals)
                    .UsingEntity<Dictionary<string, object>>(
                        "Shared1",
                        e => e.HasOne<NavDependent>().WithMany(),
                        e => e.HasOne<ManyToManyNavPrincipal>().WithMany());

                modelBuilder.Entity<ManyToManyPrincipalWithField>()
                    .HasMany(e => e.Dependents)
                    .WithMany(e => e.ManyToManyPrincipals)
                    .UsingEntity<Dictionary<string, object>>(
                        "Shared2",
                        e => e.HasOne<DependentWithField>().WithMany(),
                        e => e.HasOne<ManyToManyPrincipalWithField>().WithMany(),
                        e => e.IndexerProperty<int>("Payload"));

                var shared1 = modelBuilder.Model.FindEntityType("Shared1");
                Assert.NotNull(shared1);
                Assert.Equal(2, shared1.GetForeignKeys().Count());
                Assert.True(shared1.HasSharedClrType);
                Assert.Equal(typeof(Dictionary<string, object>), shared1.ClrType);

                var shared2 = modelBuilder.Model.FindEntityType("Shared2");
                Assert.NotNull(shared2);
                Assert.Equal(2, shared2.GetForeignKeys().Count());
                Assert.True(shared2.HasSharedClrType);
                Assert.Equal(typeof(Dictionary<string, object>), shared2.ClrType);
                Assert.NotNull(shared2.FindProperty("Payload"));

                Assert.Equal(
                    CoreStrings.ClashingSharedType(typeof(Dictionary<string, object>).DisplayName()),
                    Assert.Throws<InvalidOperationException>(() => modelBuilder.Entity<Dictionary<string, object>>()).Message);
            }

            [ConditionalFact]
            public virtual void UsingEntity_with_shared_type_fails_when_not_marked()
            {
                var modelBuilder = CreateModelBuilder();

                Assert.Equal(
                    CoreStrings.TypeNotMarkedAsShared(typeof(ManyToManyJoinWithFields).DisplayName()),
                    Assert.Throws<InvalidOperationException>(
                        () => modelBuilder.Entity<ManyToManyPrincipalWithField>()
                            .HasMany(e => e.Dependents)
                            .WithMany(e => e.ManyToManyPrincipals)
                            .UsingEntity<ManyToManyJoinWithFields>(
                                "Shared",
                                r => r.HasOne<DependentWithField>().WithMany(),
                                l => l.HasOne<ManyToManyPrincipalWithField>().WithMany())).Message);
            }

            [ConditionalFact]
            public virtual void UsingEntity_with_shared_type_passed_when_marked_as_shared_type()
            {
                var modelBuilder = CreateModelBuilder();

                modelBuilder.SharedTypeEntity<ManyToManyJoinWithFields>("Shared");

                var joinEntityType = modelBuilder.Entity<ManyToManyPrincipalWithField>()
                    .HasMany(e => e.Dependents)
                    .WithMany(e => e.ManyToManyPrincipals)
                    .UsingEntity<ManyToManyJoinWithFields>(
                        "Shared",
                        r => r.HasOne<DependentWithField>().WithMany(),
                        l => l.HasOne<ManyToManyPrincipalWithField>().WithMany()).Metadata;

                Assert.True(joinEntityType.HasSharedClrType);
                Assert.Equal("Shared", joinEntityType.Name);
                Assert.Equal(typeof(ManyToManyJoinWithFields), joinEntityType.ClrType);
            }

            [ConditionalFact]
            public virtual void UsingEntity_with_shared_type_passes_when_configured_as_shared()
            {
                var modelBuilder = CreateModelBuilder();

                modelBuilder.SharedTypeEntity<ManyToManyJoinWithFields>("Shared");

                var joinEntityType = modelBuilder.Entity<ManyToManyPrincipalWithField>()
                    .HasMany(e => e.Dependents)
                    .WithMany(e => e.ManyToManyPrincipals)
                    .UsingEntity<ManyToManyJoinWithFields>(
                        "Shared",
                        r => r.HasOne<DependentWithField>().WithMany(),
                        l => l.HasOne<ManyToManyPrincipalWithField>().WithMany()).Metadata;

                Assert.True(joinEntityType.HasSharedClrType);
                Assert.Equal("Shared", joinEntityType.Name);
                Assert.Equal(typeof(ManyToManyJoinWithFields), joinEntityType.ClrType);
            }
        }
    }
}
