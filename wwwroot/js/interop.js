function getElementWidth(element) {
    if (element) {
        return element.clientWidth;
    }
    return 0;
}

const observers = new Map();

function debounce(func, delay) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), delay);
    };
}

// This function now observes the BODY element for resize events.
function observeResize(dotNetObjectReference, element) {
    if (!element) return;

    const debouncedCallback = debounce(() => {
        // We always report the body's clientWidth
        dotNetObjectReference.invokeMethodAsync('OnElementResized', document.body.clientWidth);
    }, 50); // 50ms delay to allow the layout to settle

    // We use a ResizeObserver on the body to detect changes.
    const observer = new ResizeObserver(debouncedCallback);
    observer.observe(document.body);

    // We still use the component's element as a key to manage observers.
    observers.set(element, observer);

    // Initial call to set the size right away.
    debouncedCallback();
}

function unobserveResize(element) {
    const observer = observers.get(element);
    if (observer) {
        observer.disconnect();
        observers.delete(element);
    }
}

// New function to get the body width on demand
function getBodyWidth() {
    return document.body.clientWidth;
}