using System.ComponentModel.DataAnnotations;

public class AskViewModel
{
    [Display(Name = "Intrebarea")]
    public string Question { get; set; } = "";

    public string? Answer { get; set; }

    public List<DocumentHit> Sources { get; set; } = [];

    public long Milliseconds { get; set; }
    [Display(Name = "Document")]
    public string Source { get; set; } = "regulament";
}