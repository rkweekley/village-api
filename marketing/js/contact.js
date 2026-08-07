document.getElementById('contact-form').addEventListener('submit', async function(e) {
  e.preventDefault();
  var form = e.target;
  var status = document.getElementById('form-status');

  var data = {
    name: form.name.value.trim(),
    email: form.email.value.trim(),
    subject: form.subject.value.trim(),
    message: form.message.value.trim()
  };

  // Client-side validation
  if (!data.name || !data.email || !data.subject || !data.message) {
    status.textContent = 'Please fill in all fields.';
    status.className = 'form-status error';
    return;
  }
  if (data.email.indexOf('@') === -1) {
    status.textContent = 'Please enter a valid email address.';
    status.className = 'form-status error';
    return;
  }

  status.textContent = 'Sending...';
  status.className = 'form-status info';

  try {
    var res = await fetch('https://my.villagefamily.app/api/auth/contact', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    });

    if (res.ok) {
      status.textContent = "Message sent! We'll get back to you soon.";
      status.className = 'form-status success';
      form.reset();
    } else {
      var err = await res.json().catch(function() { return {}; });
      status.textContent = err.error || 'Something went wrong. Please try again.';
      status.className = 'form-status error';
    }
  } catch (err) {
    status.textContent = 'Network error. Please try again.';
    status.className = 'form-status error';
  }
});
