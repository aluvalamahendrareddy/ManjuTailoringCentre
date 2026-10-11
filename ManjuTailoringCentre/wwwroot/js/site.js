$(document).ready(function () {
    // Navbar scroll effect
    $(window).scroll(function () {
        if ($(this).scrollTop() > 50) {
            $('.navbar').addClass('sticky-nav');
        } else {
            $('.navbar').removeClass('sticky-nav');
        }
    });

    // Fade-up animations
    function checkVisibility() {
        $('.fade-up').each(function () {
            var bottom_of_object = $(this).offset().top + $(this).outerHeight() / 4;
            var bottom_of_window = $(window).scrollTop() + $(window).height();
            if (bottom_of_window > bottom_of_object) {
                $(this).addClass('visible');
            }
        });
    }

    $(window).scroll(function () {
        checkVisibility();
    });

    // Initial check on page load
    checkVisibility();

    // Smooth scroll
    $('a[href^="#"]').on('click', function (event) {
        var target = $(this.getAttribute('href'));
        if (target.length) {
            event.preventDefault();
            $('html, body').stop().animate({
                scrollTop: target.offset().top - 80
            }, 800);
        }
    });
});
