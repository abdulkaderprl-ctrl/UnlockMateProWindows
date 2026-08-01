using System.Windows.Input;

namespace UnlockMatePro.ViewModels
{
    public class BrandPlaceholderViewModel : ViewModelBase
    {
        private string _brandName;
        public string BrandName
        {
            get => _brandName;
            set => SetProperty(ref _brandName, value);
        }

        public BrandPlaceholderViewModel(string brandName)
        {
            _brandName = brandName;
        }
    }
}
