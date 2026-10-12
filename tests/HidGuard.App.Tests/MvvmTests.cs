using Shouldly;
using Xunit;
using HidGuard.App.Mvvm;

namespace HidGuard.App.Tests;

public sealed class MvvmTests
{
    private sealed class TestViewModel : ObservableObject
    {
        private string name = string.Empty;

        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }
    }

    [Fact]
    public void ObservableObjectShouldRaisePropertyChangedWhenValueChanges()
    {
        var vm = new TestViewModel();
        string? changedProperty = null;
        vm.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        vm.Name = "NewValue";

        changedProperty.ShouldBe(nameof(TestViewModel.Name));
        vm.Name.ShouldBe("NewValue");
    }

    [Fact]
    public void RelayCommandShouldExecuteAction()
    {
        var executed = false;
        var command = new RelayCommand(() => executed = true);

        command.CanExecute(null).ShouldBeTrue();
        command.Execute(null);

        executed.ShouldBeTrue();
    }

    [Fact]
    public void RelayCommandGenericShouldPassParameter()
    {
        string? received = null;
        var command = new RelayCommand<string>(param => received = param);

        command.Execute("TestParameter");

        received.ShouldBe("TestParameter");
    }

    [Fact]
    public async Task AsyncRelayCommandShouldExecuteTask()
    {
        var executed = false;
        var command = new AsyncRelayCommand(async () =>
        {
            await Task.Yield();
            executed = true;
        });

        command.CanExecute(null).ShouldBeTrue();
        command.Execute(null);

        await Task.Delay(50);
        executed.ShouldBeTrue();
    }
}
