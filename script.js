function przetworzNumer() {
    const haslo = document.getElementById('haslo').value;
    const numer = document.getElementById('numer').value;
    const zmodyfikowanyNumer = numer.replace(/,/g, ',Del=48');
    const wynik = `#${haslo},DEL=${zmodyfikowanyNumer}`;
    document.getElementById('wynik').innerText = wynik;
}

function skopiujDoSchowka() {
    const wynik = document.getElementById('wynik').innerText;
    if (wynik) {
        navigator.clipboard.writeText(wynik)
            .then(() => alert('Skopiowano! Wynik został skopiowany do schowka.'))
            .catch(() => alert('Błąd kopiowania!'));
    } else {
        alert('Brak wyniku do skopiowania!');
    }
}