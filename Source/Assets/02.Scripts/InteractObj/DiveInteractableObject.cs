using System;

public class DiveInteractableObject : InteractObject
{
    public event Action DiveRequested;

    protected override bool ApplyInteraction(Hero interactor)
    {
        if (DiveRequested == null) return false;

        DiveRequested.Invoke();
        return true;
    }
}
