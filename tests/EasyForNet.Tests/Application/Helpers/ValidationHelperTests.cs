using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EasyForNet.Application.Helpers;
using EasyForNet.Tests.Base;
using FluentValidation;
using Xunit;
using Xunit.Abstractions;
using ValidationException = EasyForNet.Exceptions.UserFriendly.ValidationException;

namespace EasyForNet.Tests.Application.Helpers
{
    public class ValidationHelperTests : TestsBase
    {
        public ValidationHelperTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void ValidateUsingAttributesTest()
        {
            var exception = Assert.Throws<ValidationException>(() =>
                ValidatorHelper.Validate(new ProductModel
                {
                    Model = string.Empty
                }));

            Assert.Equal(3, exception.ValidationErrors.Count);

            Assert.Equal(nameof(ProductModel.Model), exception.ValidationErrors[0].PropertyName);
            Assert.Equal(nameof(ProductModel.Color), exception.ValidationErrors[1].PropertyName);
            Assert.Equal(nameof(ProductModel.Weight), exception.ValidationErrors[2].PropertyName);

            Assert.Equal($"The {nameof(ProductModel.Model)} field is required.",
                exception.ValidationErrors[0].ErrorMessage);
            Assert.Equal($"The {nameof(ProductModel.Color)} field is required.",
                exception.ValidationErrors[1].ErrorMessage);
            Assert.Equal($"The field {nameof(ProductModel.Weight)} must be between 1 and 100.",
                exception.ValidationErrors[2].ErrorMessage);
        }

        [Fact]
        public void ValidateListUsingAttributesTest()
        {
            var exception = Assert.Throws<ValidationException>(() =>
                ValidatorHelper.Validate(new List<ProductModel>
                {
                    new(), new()
                }));

            Assert.Equal(6, exception.ValidationErrors.Count);

            Assert.Equal($"[0].{nameof(ProductModel.Model)}", exception.ValidationErrors[0].PropertyName);
            Assert.Equal($"[0].{nameof(ProductModel.Color)}", exception.ValidationErrors[1].PropertyName);
            Assert.Equal($"[0].{nameof(ProductModel.Weight)}", exception.ValidationErrors[2].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Model)}", exception.ValidationErrors[3].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Color)}", exception.ValidationErrors[4].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Weight)}", exception.ValidationErrors[5].PropertyName);

            Assert.Equal($"The {nameof(ProductModel.Model)} field is required.",
                exception.ValidationErrors[0].ErrorMessage);
            Assert.Equal($"The {nameof(ProductModel.Color)} field is required.",
                exception.ValidationErrors[1].ErrorMessage);
            Assert.Equal($"The field {nameof(ProductModel.Weight)} must be between 1 and 100.",
                exception.ValidationErrors[2].ErrorMessage);
            Assert.Equal($"The {nameof(ProductModel.Model)} field is required.",
                exception.ValidationErrors[3].ErrorMessage);
            Assert.Equal($"The {nameof(ProductModel.Color)} field is required.",
                exception.ValidationErrors[4].ErrorMessage);
            Assert.Equal($"The field {nameof(ProductModel.Weight)} must be between 1 and 100.",
                exception.ValidationErrors[5].ErrorMessage);
        }

        [Fact]
        public async Task ValidateFluentlyTest()
        {
            var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
                await ValidatorHelper.ValidateAsync(new ProductModel
                {
                    Model = string.Empty,
                    Category = new CategoryModel(),
                    Items = new List<ProductItemModel>
                    {
                        new()
                    }
                }, new ProductModelValidator()));

            Assert.Equal(5, exception.ValidationErrors.Count);

            Assert.Equal(nameof(ProductModel.Model), exception.ValidationErrors[0].PropertyName);
            Assert.Equal(nameof(ProductModel.Color), exception.ValidationErrors[1].PropertyName);
            Assert.Equal(nameof(ProductModel.Weight), exception.ValidationErrors[2].PropertyName);
            Assert.Equal($"{nameof(ProductModel.Category)}.{nameof(CategoryModel.Name)}",
                exception.ValidationErrors[3].PropertyName);
            Assert.Equal($"{nameof(ProductModel.Items)}[0].{nameof(ProductItemModel.SerialNo)}",
                exception.ValidationErrors[4].PropertyName);

            Assert.Equal("'Model' must not be empty.", exception.ValidationErrors[0].ErrorMessage);
            Assert.Equal("'Color' must not be empty.", exception.ValidationErrors[1].ErrorMessage);
            Assert.Equal("'Weight' must be between 1 and 100. You entered 0.",
                exception.ValidationErrors[2].ErrorMessage);
            Assert.Equal("'Name' must not be empty.", exception.ValidationErrors[3].ErrorMessage);
            Assert.Equal("'Serial No' must not be empty.", exception.ValidationErrors[4].ErrorMessage);
        }

        [Fact]
        public async Task ValidateListFluentlyTest()
        {
            var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
                await ValidatorHelper.ValidateAsync(new List<ProductModel>
                {
                    new()
                    {
                        Category = new(),
                        Items = new List<ProductItemModel>
                        {
                            new()
                        }
                    },
                    new()
                    {
                        Category = new(),
                        Items = new List<ProductItemModel>
                        {
                            new()
                        }
                    }
                }, new ProductModelValidator()));

            Assert.Equal(10, exception.ValidationErrors.Count);

            Assert.Equal($"[0].{nameof(ProductModel.Model)}", exception.ValidationErrors[0].PropertyName);
            Assert.Equal($"[0].{nameof(ProductModel.Color)}", exception.ValidationErrors[1].PropertyName);
            Assert.Equal($"[0].{nameof(ProductModel.Weight)}", exception.ValidationErrors[2].PropertyName);
            Assert.Equal($"[0].{nameof(ProductModel.Category)}.{nameof(CategoryModel.Name)}",
                exception.ValidationErrors[3].PropertyName);
            Assert.Equal($"[0].{nameof(ProductModel.Items)}[0].{nameof(ProductItemModel.SerialNo)}",
                exception.ValidationErrors[4].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Model)}", exception.ValidationErrors[5].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Color)}", exception.ValidationErrors[6].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Weight)}", exception.ValidationErrors[7].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Category)}.{nameof(CategoryModel.Name)}",
                exception.ValidationErrors[8].PropertyName);
            Assert.Equal($"[1].{nameof(ProductModel.Items)}[0].{nameof(ProductItemModel.SerialNo)}",
                exception.ValidationErrors[9].PropertyName);

            Assert.Equal("'Model' must not be empty.", exception.ValidationErrors[0].ErrorMessage);
            Assert.Equal("'Color' must not be empty.", exception.ValidationErrors[1].ErrorMessage);
            Assert.Equal("'Weight' must be between 1 and 100. You entered 0.",
                exception.ValidationErrors[2].ErrorMessage);
            Assert.Equal("'Name' must not be empty.", exception.ValidationErrors[3].ErrorMessage);
            Assert.Equal("'Serial No' must not be empty.", exception.ValidationErrors[4].ErrorMessage);
            Assert.Equal("'Model' must not be empty.", exception.ValidationErrors[5].ErrorMessage);
            Assert.Equal("'Color' must not be empty.", exception.ValidationErrors[6].ErrorMessage);
            Assert.Equal("'Weight' must be between 1 and 100. You entered 0.",
                exception.ValidationErrors[7].ErrorMessage);
            Assert.Equal("'Name' must not be empty.", exception.ValidationErrors[8].ErrorMessage);
            Assert.Equal("'Serial No' must not be empty.", exception.ValidationErrors[9].ErrorMessage);
        }
    }

    public class ProductModel
    {
        [Required] public string Model { get; set; }

        [Required] public string Color { get; set; }

        [Range(1, 100)] public float Weight { get; set; }

        public CategoryModel Category { get; set; }

        public List<ProductItemModel> Items { get; set; }
    }

    public class CategoryModel
    {
        [Required] public string Name { get; set; }
    }

    public class ProductItemModel
    {
        [Required] public string SerialNo { get; set; }
    }

    public class ProductModelValidator : AbstractValidator<ProductModel>
    {
        public ProductModelValidator()
        {
            RuleFor(p => p.Model)
                .NotEmpty();
            RuleFor(p => p.Color)
                .NotEmpty();
            RuleFor(p => p.Weight)
                .InclusiveBetween(1, 100);
            RuleFor(p => p.Category)
                .NotNull()
                .SetValidator(new CategoryModelValidator());
            RuleForEach(p => p.Items)
                .SetValidator(new ProductItemModelValidator());
        }
    }

    public class CategoryModelValidator : AbstractValidator<CategoryModel>
    {
        public CategoryModelValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty();
        }
    }

    public class ProductItemModelValidator : AbstractValidator<ProductItemModel>
    {
        public ProductItemModelValidator()
        {
            RuleFor(pi => pi.SerialNo)
                .NotEmpty();
        }
    }
}