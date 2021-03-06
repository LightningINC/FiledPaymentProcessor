﻿using AutoMapper;
using FiledPaymentProcessor.Core.DTOs;
using FiledPaymentProcessor.Core.Entities;
using FiledPaymentProcessor.Core.Repositories;
using FiledPaymentProcessor.Core.Services.PaymentGateway;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace FiledPaymentProcessor.Core.Services
{
    public class PaymentService : IPaymentService
    {
        IPaymentGateway paymentGateway;
        public const decimal CHEAP_PAYMENT_DEFAULT_RANGE = 20;
        public const decimal EXPENSIVE_PAYMENT_DEFAULT_RANGE = 500;

        public IUnitOfWork _unitOfWork;
        public IMapper _mapper;
        public ILogger<PaymentService> _logger;
        public IPaymentGatewayFactory _paymentGatewayFactory;
        public PaymentService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<PaymentService> logger, IPaymentGatewayFactory paymentGatewayFactory)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _paymentGatewayFactory = paymentGatewayFactory;
        }
        public async Task<PaymentResponseDTO> ProcessPaymentRequest(PaymentRequest req)
        {
            _logger.LogInformation($"request object in payment service:::: {JsonConvert.SerializeObject(req)}");
            var payment = _mapper.Map<PaymentDetail>(req);

            await _unitOfWork.Payments.Add(payment);
            var paymentStatus = new PaymentStatus
            {
                PaymentDetailId = payment.Id,
                State = PaymentState.PENDING
            };
            await _unitOfWork.PaymentState.Add(paymentStatus);
            await _unitOfWork.Complete();

            decimal amount = req.Amount;
            if (amount <= CHEAP_PAYMENT_DEFAULT_RANGE) paymentGateway = _paymentGatewayFactory.CreateCheapGatewayCheapService();
            else if (amount <= EXPENSIVE_PAYMENT_DEFAULT_RANGE) paymentGateway = _paymentGatewayFactory.CreateExpensiveGatewayExpensiveService();
            else if (amount > EXPENSIVE_PAYMENT_DEFAULT_RANGE) paymentGateway = _paymentGatewayFactory.CreateExpensiveGatewayPremiumService();
            
            var paymentProcessingResult = await paymentGateway.PaymentProcessor(req);
            if (!paymentProcessingResult) paymentStatus.State = PaymentState.FAILED;
            else paymentStatus.State = PaymentState.PROCESSED;

            _unitOfWork.PaymentState.Update(paymentStatus);
            await _unitOfWork.Complete();

            var paymentResponse = _mapper.Map<PaymentResponseDTO>(payment);
            _logger.LogInformation($"response object in payment service:::: {JsonConvert.SerializeObject(paymentResponse)}");

            return paymentResponse;
        }
    }
}
