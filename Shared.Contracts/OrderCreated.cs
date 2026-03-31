using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Contracts;
public record OrderCreated(Guid OrderId, int CustomerId, decimal TotalAmount);
