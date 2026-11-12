namespace Birko.Fakturoid.ValueLists
{
    // Values documented by Fakturoid; strings, so a value Fakturoid adds later still works.

    // Request.Subject.Type
    public static class SubjectType
    {
        public const string Customer = "customer";
        public const string Supplier = "supplier";
        public const string Both = "both";
    }

    // Subject settings that override the account (setting_*).
    public static class SettingValue
    {
        public const string Inherit = "inherit";
        public const string On = "on";
        public const string Off = "off";
    }

    // Request.Invoice.DocumentType
    public static class DocumentType
    {
        public const string Invoice = "invoice";
        public const string Proforma = "proforma";
        // Legacy, cannot be set for new documents.
        public const string PartialProforma = "partial_proforma";
        public const string Correction = "correction";
        public const string TaxDocument = "tax_document";
        public const string FinalInvoice = "final_invoice";
    }

    // InvoiceFilter.DocumentType
    public static class DocumentTypeFilter
    {
        // All documents except proformas.
        public const string Regular = "regular";
        public const string Proforma = "proforma";
        public const string Invoice = "invoice";
        public const string Correction = "correction";
        public const string TaxDocument = "tax_document";
    }

    // Response.Invoice.Status, InvoiceFilter.Status
    public static class InvoiceStatus
    {
        public const string Open = "open";
        public const string Sent = "sent";
        public const string Overdue = "overdue";
        public const string Paid = "paid";
        // VAT payers only.
        public const string Cancelled = "cancelled";
        public const string Uncollectible = "uncollectible";
    }

    // Invoices.Fire
    public static class InvoiceAction
    {
        public const string MarkAsSent = "mark_as_sent";
        public const string Cancel = "cancel";
        public const string UndoCancel = "undo_cancel";
        public const string Lock = "lock";
        public const string Unlock = "unlock";
        public const string MarkAsUncollectible = "mark_as_uncollectible";
        public const string UndoUncollectible = "undo_uncollectible";
    }

    // Request.Invoice.ProformaFollowupDocument, Request.InvoicePayment.ProformaFollowupDocument
    public static class ProformaFollowupDocument
    {
        public const string FinalInvoicePaid = "final_invoice_paid";
        public const string FinalInvoice = "final_invoice";
        public const string TaxDocument = "tax_document";
        public const string None = "none";
    }

    // Request.Invoice.PaymentMethod
    public static class PaymentMethod
    {
        public const string Bank = "bank";
        public const string Cash = "cash";
        public const string CashOnDelivery = "cod";
        public const string Card = "card";
        public const string Paypal = "paypal";
        // With Request.Invoice.CustomPaymentMethod.
        public const string Custom = "custom";
    }

    // Request.Invoice.Language, Request.Subject.Language
    public static class Language
    {
        public const string Czech = "cz";
        public const string Slovak = "sk";
        public const string English = "en";
        public const string German = "de";
        public const string French = "fr";
        public const string Italian = "it";
        public const string Spanish = "es";
        public const string Russian = "ru";
        public const string Polish = "pl";
        public const string Hungarian = "hu";
        public const string Romanian = "ro";
    }

    // Request.Invoice.VatPriceMode
    public static class VatPriceMode
    {
        public const string WithoutVat = "without_vat";
        public const string FromTotalWithVat = "from_total_with_vat";
    }

    // Request.Invoice.Oss
    public static class Oss
    {
        public const string Disabled = "disabled";
        public const string Service = "service";
        public const string Goods = "goods";
    }

    // Request.Invoice.IbanVisibility
    public static class IbanVisibility
    {
        public const string Automatically = "automatically";
        public const string Always = "always";
    }

    // Request.Invoice.Theme
    public static class Theme
    {
        public const string Solaris = "solaris";
        public const string Aurora = "aurora";
        public const string Phoenix = "phoenix";
        public const string Orion = "orion";
        public const string Lyra = "lyra";
        public const string Vega = "vega";
    }

    // Request.Webhook.Events, WebhookPayload.EventName
    public static class WebhookEvent
    {
        public const string InvoiceCreated = "invoice_created";
        public const string InvoiceUpdated = "invoice_updated";
        public const string InvoiceRemoved = "invoice_removed";
        public const string InvoiceRestored = "invoice_restored";
        public const string InvoiceOverdue = "invoice_overdue";
        public const string InvoicePaid = "invoice_paid";
        public const string InvoicePaymentAdded = "invoice_payment_added";
        public const string InvoicePaymentRemoved = "invoice_payment_removed";
        public const string InvoiceSent = "invoice_sent";
        public const string InvoiceLocked = "invoice_locked";
        public const string InvoiceUnlocked = "invoice_unlocked";
        public const string InvoiceCancelled = "invoice_cancelled";
        public const string InvoiceCancellationRemoved = "invoice_cancellation_removed";
        public const string InvoiceUncollectible = "invoice_uncollectible";
        public const string InvoiceUncollectibleRemoved = "invoice_uncollectible_removed";

        public const string GeneratorCreated = "generator_created";
        public const string GeneratorUpdated = "generator_updated";
        public const string GeneratorRemoved = "generator_removed";
        public const string GeneratorRestored = "generator_restored";

        public const string RecurringGeneratorCreated = "recurring_generator_created";
        public const string RecurringGeneratorUpdated = "recurring_generator_updated";
        public const string RecurringGeneratorRemoved = "recurring_generator_removed";
        public const string RecurringGeneratorRestored = "recurring_generator_restored";
        public const string RecurringGeneratorInvoiceCreated = "recurring_generator_invoice_created";
        public const string RecurringGeneratorPaused = "recurring_generator_paused";

        public const string ExpenseCreated = "expense_created";
        public const string ExpenseUpdated = "expense_updated";
        public const string ExpenseRemoved = "expense_removed";
        public const string ExpenseRestored = "expense_restored";
        public const string ExpenseOverdue = "expense_overdue";
        public const string ExpensePaid = "expense_paid";
        public const string ExpensePaymentAdded = "expense_payment_added";
        public const string ExpensePaymentRemoved = "expense_payment_removed";
        public const string ExpenseLocked = "expense_locked";
        public const string ExpenseUnlocked = "expense_unlocked";

        public const string InboxFileCreated = "inbox_file_created";
        public const string InboxFileUpdated = "inbox_file_updated";
        public const string InboxFileRemoved = "inbox_file_removed";
        public const string InboxFileRemovedAll = "inbox_file_removed_all";
        public const string InboxFileOcrProcessingFailed = "inbox_file_ocr_processing_failed";
        public const string InboxFileOcrProcessingRejected = "inbox_file_ocr_processing_rejected";
        public const string InboxFileOcrProcessed = "inbox_file_ocr_processed";

        public const string InventoryItemCreated = "inventory_item_created";
        public const string InventoryItemUpdated = "inventory_item_updated";
        public const string InventoryItemRemoved = "inventory_item_removed";
        public const string InventoryItemArchived = "inventory_item_archived";
        public const string InventoryItemUnarchived = "inventory_item_unarchived";

        public const string InventoryMoveCreated = "inventory_move_created";
        public const string InventoryMoveUpdated = "inventory_move_updated";
        public const string InventoryMoveRemoved = "inventory_move_removed";

        public const string SubjectCreated = "subject_created";
        public const string SubjectUpdated = "subject_updated";
        public const string SubjectRemoved = "subject_removed";
    }
}
