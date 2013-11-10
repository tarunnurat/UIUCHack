/* 
Need any help for Sell Anything template? : Contact
=============================
-----------------------------
	bootstrappage.com
	Frontend Web Development
	Email	: info@bootstrappage.com
			: bootstrappage@gmail.com
-----------------------------
=============================
 */
$(document).ready(function() {
	$("body").css("display", "none");
    $("body").fadeIn(500);
			
/* Home page item price animation---------------------------------------------*/
$('#featuredProduct .well').mouseenter(function() {
  $(this).children('.price').fadeOut();
   $(this).children('.priceTag').fadeIn();
  $(this).children('.priceTag').animate({
	 opacity:1
  }, 150, function() {
    // Animation complete.
$(this).children('.overlay').fadeIn(150);
  });
});

$('#featuredProduct .well').mouseleave(function() {
	$(this).children('.price').fadeIn();
	$(this).children('.priceTag').animate({
	opacity:0
  }, 400, function() {
    // Animation complete.
	$(this).children('.overlay').fadeOut(50)
  });
});

	  
/* Home page main carousel---------------- */
	$('#myCarousel.carousel').carousel({
	  interval: 7000
	})
	
/* New items carousel---------------- */
	$('#newCarousel .carousel').carousel({
	  interval: 4000
	})

/* Detail page carousel---------------- */
	$('#detailViewCarousel.carousel').carousel({
	  interval: 7000
	})
	
	
	$('#showCase .carousel').carousel({
	  interval: 3000
	})
 
	/* Home page main carousel for clothes---------------- */
	$('#myCarouselCloth .carousel').carousel({
	  interval: 6000
	})
/* Clients carousel---------------- */
	$('#clients').carousel({
	  interval: 6000
	})
/* Blog carousel---------------- */
	$('#myCarouselOne').carousel({
	  interval: 5000
	})
	
/* Text blink-------------- */
	$('.blink').each(function() {
    var elem = $(this);
    setInterval(function() {
        if (elem.css('visibility') == 'hidden') {
            elem.css('visibility', 'visible');
        } else {
            elem.css('visibility', 'hidden');
        }    
    }, 400);
});
/* Go to top */
$(function() {
	$(window).scroll(function() {
		if($(this).scrollTop() > 50) {
			$('#toTop').fadeIn();	
		} else {
			$('#toTop').fadeOut();
		}
	});
 
	$('#toTop').click(function() {
		$('body,html').animate({scrollTop:0},800);
	});	
});
	
});