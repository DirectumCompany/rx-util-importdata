﻿using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using System.Linq;
using Sungero.Domain.Client;
using Sungero.Domain.ClientLinqExpressions;

namespace ImportData
{
  class IncomingLetter : Entity
  {
    public int PropertiesCount = 12;
    /// <summary>
    /// Получить наименование число запрашиваемых параметров.
    /// </summary>
    /// <returns>Число запрашиваемых параметров.</returns>
    public override int GetPropertiesCount()
    {
      return PropertiesCount;
    }

    /// <summary>
    /// Сохранение сущности в RX.
    /// </summary>
    /// <param name="shift">Сдвиг по горизонтали в XLSX документе. Необходим для обработки документов, составленных из элементов разных сущностей.</param>
    /// <param name="logger">Логировщик.</param>
    /// <returns>Число запрашиваемых параметров.</returns>
    public override IEnumerable<Structures.ExceptionsStruct> SaveToRX(NLog.Logger logger, bool supplementEntity, int shift = 0)
    {
      var exceptionList = new List<Structures.ExceptionsStruct>();

      using (var session = new Session())
      {
        var regNumber = this.Parameters[shift + 0];
        var regDate = DateTime.MinValue;
        var style = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        var culture = CultureInfo.CreateSpecificCulture("en-GB");
        var regDateDouble = 0.0;
        if (!string.IsNullOrWhiteSpace(this.Parameters[shift + 1]) && !double.TryParse(this.Parameters[shift + 1].Trim(), style, culture, out regDateDouble))
        {
          var message = string.Format("Не удалось обработать дату регистрации \"{0}\".", this.Parameters[shift + 1]);
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          logger.Error(message);
          return exceptionList;
        }
        else
        {
          if (!string.IsNullOrEmpty(this.Parameters[shift + 1].ToString()))
            regDate = DateTime.FromOADate(regDateDouble);
        }
        var counterparty = BusinessLogic.GetConterparty(session, this.Parameters[shift + 2], exceptionList, logger);
        if (counterparty == null)
        {
          var message = string.Format("Не найден контрагент \"{0}\".", this.Parameters[shift + 2]);
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          logger.Error(message);
          return exceptionList;
        }

        var documentKind = BusinessLogic.GetDocumentKind(session, this.Parameters[shift + 3], exceptionList, logger);
        if (documentKind == null)
        {
          var message = string.Format("Не найден вид документа \"{0}\".", this.Parameters[shift + 3]);
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          logger.Error(message);
          return exceptionList;
        }

        var subject = this.Parameters[shift + 4];

        var department = BusinessLogic.GetDepartment(session, this.Parameters[shift + 5], exceptionList, logger);
        if (department == null)
        {
          var message = string.Format("Не найдено подразделение \"{0}\".", this.Parameters[shift + 5]);
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
          logger.Error(message);
          return exceptionList;
        }

        var filePath = this.Parameters[shift + 6];

        var dated = DateTime.MinValue;
        var datedDouble = 0.0;
        if (!string.IsNullOrWhiteSpace(this.Parameters[shift + 7]) && !double.TryParse(this.Parameters[shift + 7].Trim(), style, culture, out datedDouble))
        {
          var message = string.Format("Не удалось обработать значение в поле \"Письмо от\" \"{0}\".", this.Parameters[shift + 7]);
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Error, Message = message });
          logger.Error(message);
          return exceptionList;
        }
        else
        {
          if (!string.IsNullOrEmpty(this.Parameters[shift + 7].ToString()))
            dated = DateTime.FromOADate(datedDouble);
        }

        var inNumber = this.Parameters[shift + 8];

        var addressee = BusinessLogic.GetEmployee(session, this.Parameters[shift + 9].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 9].Trim()) && addressee == null)
        {
          var message = string.Format("Не найден Адресат \"{2}\". Входящее письмо: \"{0} {1}\". ", regNumber, regDate.ToString(), this.Parameters[shift + 9].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }

        var deliveryMethod = BusinessLogic.GetMailDeliveryMethod(session, this.Parameters[shift + 10].Trim(), exceptionList, logger);
        if (!string.IsNullOrEmpty(this.Parameters[shift + 10].Trim()) && deliveryMethod == null)
        {
          var message = string.Format("Не найден Способ доставки \"{2}\". Входящее письмо: \"{0} {1}\". ", regNumber, regDate.ToString(), this.Parameters[shift + 10].Trim());
          exceptionList.Add(new Structures.ExceptionsStruct { ErrorType = Constants.ErrorTypes.Warn, Message = message });
          logger.Warn(message);
        }

        var note = this.Parameters[shift + 11];
        try
        {
          var incomingLetters = Enumerable.ToList(session.GetAll<Sungero.RecordManagement.IIncomingLetter>().Where(x => x.RegistrationNumber == regNumber && regDate != DateTime.MinValue && x.RegistrationDate == regDate));
          var incomingLetter = (Enumerable.FirstOrDefault<Sungero.RecordManagement.IIncomingLetter>(incomingLetters));
          if (incomingLetter != null)
          {
            var message = string.Format("Входящее письмо не может быть импортировано. Найден дубль с такими же реквизитами \"Дата документа\" {0} и \"Рег. №\" {1}.", regDate.ToString("d"), regNumber);
            exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
            logger.Error(message);
            return exceptionList;
          }

          incomingLetter = session.Create<Sungero.RecordManagement.IIncomingLetter>();
          if (regDate != DateTime.MinValue)
            incomingLetter.RegistrationDate = regDate;
          incomingLetter.RegistrationNumber = regNumber;
          incomingLetter.Correspondent = counterparty;
          incomingLetter.DocumentKind = documentKind;
          incomingLetter.Subject = subject;
          incomingLetter.Department = department;
          if (department != null)
            incomingLetter.BusinessUnit = department.BusinessUnit;
          if (dated != DateTime.MinValue)
            incomingLetter.Dated = dated;
          incomingLetter.InNumber = inNumber;
          incomingLetter.Addressee = addressee;
          incomingLetter.DeliveryMethod = deliveryMethod;
          incomingLetter.Note = note;
          incomingLetter.Save();
          if (!string.IsNullOrWhiteSpace(filePath))
            exceptionList.Add(BusinessLogic.ImportBody(session, incomingLetter, filePath, logger));
          var documentRegisterId = 0;
          if (ExtraParameters.ContainsKey("doc_register_id"))
            if (int.TryParse(ExtraParameters["doc_register_id"], out documentRegisterId))
              exceptionList.AddRange(BusinessLogic.RegisterDocument(session, incomingLetter, documentRegisterId, regNumber, regDate, Constants.RolesGuides.RoleIncomingDocumentsResponsible, logger));
            else
            {
              var message = string.Format("Не удалось обработать параметр \"doc_register_id\". Полученное значение: {0}.", ExtraParameters["doc_register_id"]);
              exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = message});
              logger.Error(message);
              return exceptionList;
            }
        }
        catch (Exception ex)
        {
          exceptionList.Add(new Structures.ExceptionsStruct {ErrorType = Constants.ErrorTypes.Error, Message = ex.Message});
          return exceptionList;
        }
        session.SubmitChanges();
      }
      return exceptionList;
    }
  }
}
