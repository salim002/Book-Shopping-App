using BookShoppingCartMvcUI.Services;
using BookShoppingCartMvcUI.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookShoppingCartMvcUI.Controllers;

[Authorize(Roles = nameof(Roles.Admin))]
public class BookController : Controller
{
    private readonly IBookRepository _bookRepo;
    private readonly IGenreRepository _genreRepo;
    private readonly IBlobStorageService _blobStorageService;

    public BookController(
        IBookRepository bookRepo,
        IGenreRepository genreRepo,
        IBlobStorageService blobStorageService)
    {
        _bookRepo = bookRepo;
        _genreRepo = genreRepo;
        _blobStorageService = blobStorageService;
    }

    public async Task<IActionResult> Index()
    {
        var books = await _bookRepo.GetBooks();
        return View(books);
    }

    public async Task<IActionResult> AddBook()
    {
        var genreSelectList = (await _genreRepo.GetGenres()).Select(genre => new SelectListItem
        {
            Text = genre.GenreName,
            Value = genre.Id.ToString(),
        });
        BookDTO bookToAdd = new() { GenreList = genreSelectList };
        return View(bookToAdd);
    }

    [HttpPost]
    public async Task<IActionResult> AddBook(BookDTO bookToAdd)
    {
        bookToAdd.GenreList = (await _genreRepo.GetGenres()).Select(genre => new SelectListItem
        {
            Text = genre.GenreName,
            Value = genre.Id.ToString(),
        });

        if (!ModelState.IsValid)
            return View(bookToAdd);

        try
        {
            if (bookToAdd.ImageFile != null)
            {
                if (bookToAdd.ImageFile.Length > 1 * 1024 * 1024)
                {
                    throw new InvalidOperationException("Image file cannot exceed 1 MB");
                }
                // Upload to Azure Blob Storage and get public URL
                bookToAdd.Image = await _blobStorageService.UploadFileAsync(bookToAdd.ImageFile);
            }

            Book book = new()
            {
                BookName = bookToAdd.BookName,
                AuthorName = bookToAdd.AuthorName,
                Image = bookToAdd.Image, // Store Blob URL
                GenreId = bookToAdd.GenreId,
                Price = bookToAdd.Price
            };
            await _bookRepo.AddBook(book);

            TempData["successMessage"] = "Book added successfully";
            return RedirectToAction(nameof(AddBook));
        }
        catch (Exception ex)
        {
            TempData["errorMessage"] = "Error saving data";
            return View(bookToAdd);
        }
    }

    public async Task<IActionResult> UpdateBook(int id)
    {
        var book = await _bookRepo.GetBookById(id);
        if (book == null)
        {
            TempData["errorMessage"] = $"Book with ID {id} not found";
            return RedirectToAction(nameof(Index));
        }

        var genreSelectList = (await _genreRepo.GetGenres()).Select(genre => new SelectListItem
        {
            Text = genre.GenreName,
            Value = genre.Id.ToString(),
            Selected = genre.Id == book.GenreId
        });

        BookDTO bookToUpdate = new()
        {
            Id = book.Id,
            GenreList = genreSelectList,
            BookName = book.BookName,
            AuthorName = book.AuthorName,
            GenreId = book.GenreId,
            Price = book.Price,
            Image = book.Image // Load existing image
        };
        return View(bookToUpdate);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBook(BookDTO bookToUpdate)
    {
        bookToUpdate.GenreList = (await _genreRepo.GetGenres()).Select(genre => new SelectListItem
        {
            Text = genre.GenreName,
            Value = genre.Id.ToString(),
            Selected = genre.Id == bookToUpdate.GenreId
        });

        if (!ModelState.IsValid)
            return View(bookToUpdate);

        try
        {
            string oldImageUrl = bookToUpdate.Image;
            if (bookToUpdate.ImageFile != null)
            {
                if (bookToUpdate.ImageFile.Length > 1 * 1024 * 1024)
                {
                    throw new InvalidOperationException("Image file cannot exceed 1 MB");
                }

                // Upload new image to blob storage
                bookToUpdate.Image = await _blobStorageService.UploadFileAsync(bookToUpdate.ImageFile);

                // Delete old image if it exists
                if (!string.IsNullOrEmpty(oldImageUrl))
                {
                    string oldBlobName = oldImageUrl.Split('/').Last(); // Extract blob name
                    await _blobStorageService.DeleteFileAsync(oldBlobName);
                }
            }

            Book book = new()
            {
                Id = bookToUpdate.Id,
                BookName = bookToUpdate.BookName,
                AuthorName = bookToUpdate.AuthorName,
                GenreId = bookToUpdate.GenreId,
                Price = bookToUpdate.Price,
                Image = bookToUpdate.Image
            };
            await _bookRepo.UpdateBook(book);

            TempData["successMessage"] = "Book updated successfully";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["errorMessage"] = "Error saving data";
            return View(bookToUpdate);
        }
    }

    public async Task<IActionResult> DeleteBook(int id)
    {
        try
        {
            var book = await _bookRepo.GetBookById(id);
            if (book == null)
            {
                TempData["errorMessage"] = $"Book with ID {id} not found";
            }
            else
            {
                if (!string.IsNullOrEmpty(book.Image))
                {
                    string blobName = book.Image.Split('/').Last();
                    await _blobStorageService.DeleteFileAsync(blobName);
                }
                await _bookRepo.DeleteBook(book);
            }
        }
        catch (Exception ex)
        {
            TempData["errorMessage"] = "Error deleting data";
        }
        return RedirectToAction(nameof(Index));
    }
}
