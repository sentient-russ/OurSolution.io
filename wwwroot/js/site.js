//format phone numbers
var phoneElement = document.getElementById("Phone");
if (phoneElement != null) {
    var phoneNo = phoneElement.value;
    phoneElement.addEventListener('input', function () {
        var phoneNo = phoneElement.value.replace(/\D/g, '');
        var formatNum = "";
        if (phoneNo.length <= 3) {
            formatNum = phoneNo;
        } else if (phoneNo.length <= 6) {
            formatNum = phoneNo.substr(0, 3) + '-' + phoneNo.substr(3);
        } else if (phoneNo.length <= 10) {
            formatNum = phoneNo.substr(0, 3) + '-' + phoneNo.substr(3, 3) + '-' + phoneNo.substr(6);
        } else {
            formatNum = phoneNo.substr(0, 3) + '-' + phoneNo.substr(3, 3) + '-' + phoneNo.substr(6, 4);
        }
        phoneElement.value = formatNum;
    });
}