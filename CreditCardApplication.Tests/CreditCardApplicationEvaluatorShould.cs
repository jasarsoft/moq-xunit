using System;
using System.Collections.Generic;
using CreditCardApplications;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Moq;
using Moq.Protected;
using Xunit;

namespace CreditCardApplication.Tests
{
    public class CreditCardApplicationEvaluatorShould
    {
        private CreditCardApplicationEvaluator sut;
        private Mock<IFrequentFlyerNumberValidator> mockValidator;
        
        public CreditCardApplicationEvaluatorShould()
        {
            mockValidator = new Mock<IFrequentFlyerNumberValidator>();
            mockValidator.SetupAllProperties();
            mockValidator.Setup(p => p.ServiceInformation.License.LicenseKey).Returns("OK");
            mockValidator.Setup(p => p.IsValid(It.IsAny<string>())).Returns(true);

            sut = new CreditCardApplicationEvaluator(mockValidator.Object);
        }

        [Fact]
        public void AcceptHighIncomeApplications()
        {
            var application = new CreditCardApplications.CreditCardApplication {GrossAnnualIncome = 100_000};

            CreditCardApplicationDecision decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.AutoAccepted, decision);
        }

        [Fact]
        public void ReferYoungApplications()
        {
            mockValidator.DefaultValue = DefaultValue.Mock;

            var application = new CreditCardApplications.CreditCardApplication {Age = 19};

            CreditCardApplicationDecision decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.ReferredToHuman, decision);
        }

        [Fact]
        public void DeclineLowIncomeApplications()
        {
            var application = new CreditCardApplications.CreditCardApplication
            {
                GrossAnnualIncome = 19_999,
                Age = 42,
                FrequentFlyerNumber = "y"
            };

            CreditCardApplicationDecision decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.AutoDeclined, decision);
        }

        [Fact]
        public void ReferInvalidFrequentFlyerApplications()
        {
            mockValidator.Setup(x => x.IsValid(It.IsAny<string>())).Returns(false);

            var application = new CreditCardApplications.CreditCardApplication();

            CreditCardApplicationDecision decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.ReferredToHuman, decision);
        }

        [Fact]
        public void ReferWhenLicenseKeyExpired()
        {
            mockValidator.Setup(x => x.ServiceInformation.License.LicenseKey).Returns("EXPIRED");

            var sut = new CreditCardApplicationEvaluator(mockValidator.Object);

            var application = new CreditCardApplications.CreditCardApplication {Age = 42};

            CreditCardApplicationDecision decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.ReferredToHuman, decision);
        }

        string GetLicenseKeyExpiryString()
        {
            //read from vendor-supplied constants file
            return "EXPIRED";
        }

        [Fact]
        public void UseDetailedLookupForOlderApplications()
        {
            var application = new CreditCardApplications.CreditCardApplication { Age = 30 };

            sut.Evaluate(application);

            Assert.Equal(ValidationMode.Detailed, mockValidator.Object.ValidationMode);
        }

        [Fact]
        public void ValidateFrequentFlyerNumberForLowIncomeApplications()
        {
            var application = new CreditCardApplications.CreditCardApplication()
            {
                FrequentFlyerNumber = "q"
            };

            sut.Evaluate(application);

            mockValidator.Verify(p => p.IsValid(It.IsAny<string>()), "Frequent flyer numbers should be validated.");
        }

        [Fact]
        public void ValidateFrequentFlyerNumberForLowIncomeApplicationsTimes()
        {
            var application = new CreditCardApplications.CreditCardApplication()
            {
                FrequentFlyerNumber = "q"
            };

            sut.Evaluate(application);

            mockValidator.Verify(p => p.IsValid(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void NotValidateFrequentFlyerNumberForHighIncomeApplications()
        {
            var application = new CreditCardApplications.CreditCardApplication()
            {
                GrossAnnualIncome = 100_000
            };

            sut.Evaluate(application);

            mockValidator.Verify(p => p.IsValid(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void CheckLicenseKeyForLowIncomeApplications()
        {
            var application = new CreditCardApplications.CreditCardApplication
            {
                GrossAnnualIncome = 99_000
            };

            sut.Evaluate(application);

            mockValidator.VerifyGet(p => p.ServiceInformation.License.LicenseKey, Times.Once);
        }

        [Fact]
        public void SetDetailedLookupForOlderApplications()
        {
            var application = new CreditCardApplications.CreditCardApplication
            {
                Age = 30
            };

            sut.Evaluate(application);

            mockValidator.VerifySet(p => p.ValidationMode =It.IsAny<ValidationMode>(), Times.Once);
            mockValidator.Verify(p => p.IsValid(null), Times.Once);

            mockValidator.VerifyNoOtherCalls();
        }

        [Fact]
        public void ReferWhenFrequentFlyerValidationError()
        {
            mockValidator.Setup(p => p.IsValid(It.IsAny<string>()))
                         .Throws(new Exception("Custom message"));

            var application = new CreditCardApplications.CreditCardApplication {Age = 42};

            CreditCardApplicationDecision decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.ReferredToHuman, decision);
        }

        [Fact]
        public void IncrementLookupCount()
        {
            mockValidator.Setup(p => p.IsValid(It.IsAny<string>()))
                .Returns(true)
                .Raises(p => p.ValidatorLookupPerformed += null, EventArgs.Empty);

            var application = new CreditCardApplications.CreditCardApplication
            {
                FrequentFlyerNumber = "x",
                Age = 25
            };

            sut.Evaluate(application);

            //mockValidator.Raise(p => p.ValidatorLookupPerformed += null, EventArgs.Empty);

            Assert.Equal(1, sut.ValidatorLookupCount);
        }

        [Fact]
        public void ReferInvalidFrequentFlyerApplications_ReturnValuesSequence()
        {
            mockValidator.SetupSequence(p => p.IsValid(It.IsAny<string>()))
                .Returns(false)
                .Returns(true);

            var application = new CreditCardApplications.CreditCardApplication
            {
                Age = 25
            };

            var firstDecision = sut.Evaluate(application);
            Assert.Equal(CreditCardApplicationDecision.ReferredToHuman, firstDecision);

            var secondDecision = sut.Evaluate(application);
            Assert.Equal(CreditCardApplicationDecision.AutoDeclined, secondDecision);
        }

        [Fact]
        public void ReferInvalidFrequentFlyerApplications_MultipleCallsSequence()
        {
            var frequentFlyerNumbersPassed = new List<string>();
            mockValidator.Setup(p => p.IsValid(Capture.In(frequentFlyerNumbersPassed)));

            var sut = new CreditCardApplicationEvaluator(mockValidator.Object);

            var application1 = new CreditCardApplications.CreditCardApplication { Age = 25, FrequentFlyerNumber = "aa" };
            var application2 = new CreditCardApplications.CreditCardApplication { Age = 25, FrequentFlyerNumber = "bb" };
            var application3 = new CreditCardApplications.CreditCardApplication { Age = 25, FrequentFlyerNumber = "cc" };

            sut.Evaluate(application1);
            sut.Evaluate(application2);
            sut.Evaluate(application3);

            //Assert that IsValid was called three times with "aa", "bb" and "cc"
            Assert.Equal(new List<string>{"aa","bb", "cc"}, frequentFlyerNumbersPassed);
        }

        [Fact]
        public void ReferFraudRisk()
        {
            var mockFraudLookup = new Mock<FraudLookup>();
            mockFraudLookup.Setup(p => p.IsFraudRisk(It.IsAny<CreditCardApplications.CreditCardApplication>()))
                .Returns(true);

            // "Override" sut in the class field to create a version with fraud lookup
            var sut = new CreditCardApplicationEvaluator(mockValidator.Object, mockFraudLookup.Object);
            var application = new CreditCardApplications.CreditCardApplication();

            var decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.ReferredToHumanFraudRisk, decision);
        }

        [Fact]
        public void ReferFraudRisk2()
        {
            var mockFraudLookup = new Mock<FraudLookup>();
            mockFraudLookup
                .Protected()
                .Setup<bool>("CheckApplication", ItExpr.IsAny<CreditCardApplications.CreditCardApplication>())
                .Returns(true);

            // "Override" sut in the class field to create a version with fraud lookup
            var sut = new CreditCardApplicationEvaluator(mockValidator.Object, mockFraudLookup.Object);
            var application = new CreditCardApplications.CreditCardApplication();

            CreditCardApplicationDecision decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.ReferredToHumanFraudRisk, decision);
        }

        [Fact]
        public void LinqToMocks()
        {
            var mockValidator = Mock.Of<IFrequentFlyerNumberValidator>(
                validator =>
                    validator.ServiceInformation.License.LicenseKey == "OK" &&
                    validator.IsValid(It.IsAny<string>()) == true

            );

            // "Override" sut in class field to demonstrate LINQ setup
            var sut = new CreditCardApplicationEvaluator(mockValidator);
            var application = new CreditCardApplications.CreditCardApplication() {Age = 25};

            var decision = sut.Evaluate(application);

            Assert.Equal(CreditCardApplicationDecision.AutoDeclined, decision);
        }
    }
}
 