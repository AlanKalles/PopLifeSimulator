**Concept Definitions**

**Resource**  
**Game time**: In-game time that flows automatically. Game time pauses when player activates any UI Panel. Actions in the game consume time to finish, including construction, customer actions, etc.

**Money**: The main currency in the game. At the start of Daily Settlement, if money \< 0, the player will lose and need to restart from the latest checkpoint, usually the day before. Player earns money from operating stores, completing requests, etc. Player uses money to construct 

**Fame**: A consumable resource. Earned by completing requests (significantly) and in daily settlement (subtly). Used to upgrade product category level, etc.

**Player Goal**  
**Store Star Upgrade**: \[unfinished\] The store can be upgraded from 0 stars to 5 stars. 

- **Requirement**: Each upgrade requires certain requirements, and the player can upgrade in an info panel when the requirements are all met. The requirements are usually quotas to meet, not resources to be cost. For example, total shelves built, total customer amount, total shelf level, total customer level, highest historical daily income, etc.  
- **Reward**: Each success upgrade rewards the player with a large amount of money and fame, and a special and significant shelf or facility.

**Request**: \[unfinished\]

**Daily Settlement**: \[unfinished\]

- (see more on p.3 Store \-\> Day Loop \-\> Daily Settlement)

**Store**

**Map**: Multiple unlockable areas in units of Floors. Floors are stacked and connected in an aesthetically irregular manner.

**Floor**: Each floor needs a cashier facility. Placement on the floor is grid-based.

**Shelves**: Placed on the floor, where customers buy products.  
	Shelf attributes:

- **Price**: The customer pays the player this money per product.  
- **Maintenance Fee**: Money that’s reduced at daily settlement.   
- **Stock**: The Maximum amount of products that can be bought in one day. If stock \= 0, the shelf is sold out and shut down for the rest of the day.  
- **Attractiveness:** The more attractive the shelf is, the more possible for a customer to come and buy its product. Shelf attractiveness together is the store’s total attractiveness.  
- **Category**: Customer will have an interest level in each category. The customer will visit the shelf from the highest interest level to the lowest. A category can be upgraded from Lv 1 to Lv 5 with a lot of fame and money, and higher levels will need an expensive maintenance fee. Category-level upgrade significantly boosts the attractiveness of all shelves in the category. Category includes: Lingere, Condom, Vibrator, Fleshlights, Lubricant  
- **Build Fee**: A one-time money cost when placing the building.  
- **Embarrassment Value (EV)**: Add the embarrassment value to the surrounding grid. Formula for each grid:   
  - Positive EV: result \+= min(0, EV \- Distance)  
  - Negative EV: result \+= max(0, EV \- Distance)

**Facilities**: Boost floor attributes or have special use. (A short list with varied effects.)   
	Facilities attributes:

- **Effect**  
- **Build Fee**  
- **Maintenance Fee**  
  Facilities examples:  
- **Cashier**: Each floor needs at least one cashier. Starting from the second unlocked floor, each cashier needs at least one employee.  
- **Air Conditioner**: Reduce embarrassment Level for all customers on the same floor.  
- **ATM**: Customer with a high interest, low embarrassment, and low money bag will use the ATM to restore their money bag.  
- (...)

**Environmental Embarrassment Value (EEB)**: Each grid has an EEB, affected by the surrounding environment (shelves, facilities, decorations). 

- **Store Embarrassment Heatmap**: A heat map that shows the embarrassment value of each grid.

**Blueprint**: Shelves and Facilities need blueprints to be built. Blueprint is obtained from completing requests.

**Construction Mode**: Player can place, destroy, and move shelves and facilities. Item placement is grid-based.

- **Place**: Costs a build fee and consumes one blueprint. Construction time: 15 seconds.  
- **Move**: Cost movement fee (small amount). Construction time: 5 seconds  
- **Destroy**: Return one blueprint. No money cost or returned. Instant construction.

**Day Loop**: A Game time day costs 30 seconds in real time. A day counter UI in the corner counts the date in the format: Day xx.

- **Store Hours**: 12:00 to 23:00. Time flows at normal speed. Customers AI will move and buy products in the store during store hours. The player can activate any UI   
- **Daily Settlement**: UI panel pop-up with information about the last store hours, at 10 pm. During daily settlement, the time flows quickly to 11 am, the next day, then pauses  
  - Daily Total Sale (all sales)  
  - Daily Total Expenses (all maintenance fees)  
  - Daily Income (sales \- expenses)  
  - Daily Total Customer  
  - **Fame Earned**: A reward to the player, calculated by the daily total income and the daily total customers.  
  - Competition / News / …

**Store info panel**: A UI panel that keeps track of all store information.

- **Store level**: A store level challenge from 0 stars to 5 stars. Player needs to fulfill specific requirements to upgrade the store level. Upgrading the store level will unlock blueprint and customer, reward money and fame, etc.  
- **Total store count**  
- **Total attractiveness count**  
- **Total income count**

**Customers**  
Customer Attributes:

- **Interest Levels (IL)**: Customer has different interest levels by category. This attribute will not change.  
- **Trust Point (TP)**: Trust points are slightly accumulated through successful buy, and through customer upgrades. Trust points will affect the Money bag size. Start with 0\.  
- **Embarrassment Point (EP)**: Each customer instance will have its own embarrassment level, separately. Start from 0 when the customer instance enters the store. It’s increased or decreased by the embarrassment level on the grid they stepped on each second. When the Embarrassment level is full, the customer will stop the buying action and run away embarrassedly.  
- **Money bag (MB)**: When customers use up all the money, they will leave.

**Customer Upgrade**: Customers are automatically upgraded to the next level when their request is completed. Upgrade grants customers some Interest Levels (differ between customers). Upgraded customers visit more frequently and buy more products.

- Customer Levels**:**

| Customer Type | Visit | Buy | Total IL Reward |  |
| :---- | :---- | :---- | :---- | :---- |
| **New Customer** | 1 \- 2 / day | 1 \- 2 | 1 \- 2 |  |
| **Regular** | 2 \- 4 / day | 1 \- 5 | 2 \- 3 |  |
| **Member** | 3 \- 6 / day | 1 \- 7 | 2 \- 4 |  |
| **Advocate** | 4 \- 9 / day | 1 \- 10 | 3 \- 5 |  |

**Customer AI**:   
	*Concepts*:

- **Embarrassment Point**: A customer attribute, starting with 0\. Each second, add the environmental embarrassment value of the customer’s standing grid to their embarrassment point. The customer will leave when the Embarrassment point is full. Embarrassment Point cannot be below 0\.  
- **Visit**: The frequency a customer spawns each day. The actual visit times are randomly selected in the range between the minimum visit and the maximum visit.  
- **Buy**: The range of amount of products the customer will try to purchase at one shelf  
- **Buy Amount:** The actual amount of product the customer will try to purchase at one shelf.  
- **Buy List**: A list of shelves that the customer will probably visit.   
- **Inventory**: Each customer has an inventory. The customer will hold the products in inventory in hand. If the customer bought it, remove the inventory visual and add a shopping bag visual.  
- **Time**: Time to wait for each action. Other customers trying to interact with the occupied object need to line up and wait till they’re finished.  
- **Target**: The Customer will always move towards the target.  
  *Customer AI Logic**:***  
1. **Customers randomly spawn at the entrance.** A customer spawns no less than min-visit and no more than max-visit times in a day, randomly.  
2. **Set the target to the most attractive shelf** for this customer.   
   The selection logic:   
1. Create a list of shelves which’s in the category (/categories) with the highest customer interest point.  
2. Randomly select a target shelf from the list. The weight of random selection is the attractiveness of the shelf.  
3. Handle exceptions: If the way to the shelf doesn’t exist, remove the shelf from the list. Move to 1\. (If this happens 3 times straight, the customer is annoyed and leaves.)  
4. Move to the target shelf. Wait in line.  
5. **Calculate the buy amount**. Randomly select between min-buy and max-buy.  
6. Handle exceptions: If {(buy amount) \* (shelf price) \> money bag}, then {buy amount \= money bag shelf price}. If buy amount \< 0, Customer leaves.  
7. Shelf stock \-= buy amount.   
8. **Make Payment**: Customers add the buy amount of product to the inventory. Move to the closest register. Line up, make payment.  
9. Payment \= (buy amount) \* (shelf price). Customer's money bag \-= payment. Player money \+= payment.  
10. Empty player inventory. Remove the bought shelf from the customer's buying list.  
11. **Move to step 2**, loop.

**Customer Actions**: 

- Walk speed \= \[unfinished, need tests\]. Customer speed is not changeable by the player.

| Action | Time cost (seconds) | Notes |
| :---- | :---- | :---- |
| **Move** | Distance/speed |  |
| **Wait** | (line position) \* (checkout/ buy time) |  |
| **Buy** | 1 \+ (buy amount \* 0.1) second |  |
| **Checkout** | 1 \+ (buy amount \* 0.1) second |  |

**Customer Traits**:

| Lingere | Condom | Vibrator | Fleshlights | Lubricant | BDSM | Money bag |
| ----- | ----- | :---- | ----- | ----- | ----- | ----- |
| 2 | 2 | 2 | 2 | 2 | 2 | 50 |

| Trait | Behavior Description | Notes |
| :---- | :---- | :---- |
| Shy | Max Embarrassment \- 20% |  |
| Liberal | Max Embarrassment \+ 20% |  |
| Rich | Money bag \+= 20% |  |
| Poor | Money bag \-= 20% |  |
| Cis-Woman | Fleshlight \- 2, Vibrator  \+ 2 |  |
| Cis-Man | Fleshlight \+ 2, Vibrator  \- 2 |  |
| Trans-Woman | Fleshlight \- 1, Vibrator \+ 1 |  |
| Trans-Man | Fleshlight \+ 1, Vibrator \- 1 |  |
| Gay | Condom \+= 1, Lubricant \+ 2 |  |
| Lesbian | Condom \-= 2 |  |
| Asexual | Condom \-= 100% |  |
| Straight |  |  |
| Pansexual |  |  |
| Bisexual |  |  |
| Polyamorous | Condom \+3 |  |
| Dating | Condom \+ 100% |  |
| Married | Condom \+ 50% |  |
| Single | Condom \- 100% |  |
| Kinky | Lingere \+ 2, BDSM \+1 |  |
| BDSM | BDSM \+ 100% |  |
| Vanilla | BDSM \- 100%, Lingere \- 100%, 
Vibrator \- 100%, Fleshlight \- 100% |  |
| Curious | Walk speed \- 10% |  |
| Fitness | Walk speed \+ 10% |  |
| Early Bird | Visiting Hour \= open \- 14; |  |
| Night Owl | Visiting Hour \= 21 \- closed; |  |
| Student | Visiting Hours \= 16 \- 23; Money Bag \- 40%; |  |
| Influencer | Fame \+ RandomRange(5, 20); |  |
| Freelancing | Visiting Hours \= anytime, 
Money bag \+ RandomRange(-50%,50%) |  |
| Blue Collar | Visiting Hours \= 19 \- 21, Money bag \+ 10% |  |
| Unemployed | Visiting Hours \= anytime,
Money bag \- 50%; |  |
| Secret Affair | Embarrassment \- 50%, 
Walk Speed \+ 50%,
All Interest x2, |  |
| Collector | All Interest \+ 3; Visit Frequency \+ 1 |  |
|  |  |  |
| Single | Visit Frequency+ |  |
| Couple | Condom+ lingerie+ |  |
| Gangbang | Money bag+= Visit Frequency Condom+ |  |